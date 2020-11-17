﻿using Serenity.ComponentModel;
using Serenity.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Serenity.Services
{
    public class BehaviorProvider : IBehaviorProvider
    {
        private readonly IImplicitBehaviorRegistry implicitBehaviors;
        private readonly IBehaviorFactory behaviorFactory;
        private readonly IRowFieldsProvider fieldsFactory;

        public BehaviorProvider(IImplicitBehaviorRegistry implicitBehaviors,
            IBehaviorFactory behaviorFactory, IRowFieldsProvider fieldsFactory)
        {
            this.implicitBehaviors = implicitBehaviors ??
                throw new ArgumentNullException(nameof(implicitBehaviors));

            this.behaviorFactory = behaviorFactory ?? 
                throw new ArgumentNullException(nameof(behaviorFactory));

            this.fieldsFactory = fieldsFactory ??
                throw new ArgumentNullException(nameof(fieldsFactory));
        }

        public IEnumerable Resolve(Type handlerType, Type rowType, Type behaviorType)
        {
            var list = new List<object>();

            var fields = fieldsFactory.Resolve(rowType);
            var row = (IRow)Activator.CreateInstance(rowType, fields);

            foreach (var type in implicitBehaviors.GetTypes())
            {
                var behavior = behaviorFactory.Create(type);
                if (behavior == null)
                    continue;

                var implicitBehavior = behavior as IImplicitBehavior;
                if (implicitBehavior == null)
                    continue;

                var fieldBehavior = behavior as IFieldBehavior;
                if (fieldBehavior == null)
                {
                    if (implicitBehavior.ActivateFor(row))
                        list.Add(behavior);

                    continue;
                }

                foreach (var field in row.GetFields())
                {
                    fieldBehavior.Target = field;
                    if (implicitBehavior.ActivateFor(row))
                    {
                        list.Add(behavior);

                        behavior = behaviorFactory.Create(behaviorType);
                        implicitBehavior = behavior as IImplicitBehavior;
                        fieldBehavior = behavior as IFieldBehavior;
                    }
                }
            }

            foreach (var attr in row.GetType().GetCustomAttributes<AddBehaviorAttribute>())
            {
                if (behaviorType.IsAssignableFrom(attr.Value))
                    list.Add(behaviorFactory.Create(attr.Value));
            }

            foreach (var field in row.GetFields())
            {
                if (field.CustomAttributes == null)
                    continue;

                foreach (var attr in field.CustomAttributes.OfType<AddBehaviorAttribute>())
                {
                    if (behaviorType.IsAssignableFrom(attr.Value) &&
                        typeof(IFieldBehavior).IsAssignableFrom(attr.Value))
                    {
                        var fieldBehavior = (IFieldBehavior)behaviorFactory.Create(attr.Value);
                        fieldBehavior.Target = field;
                        list.Add(fieldBehavior);
                    }
                }
            }

            return list;
        }
    }
}