using System.Globalization;
using OmniXaml.TypeConversion;

namespace OmniXaml.NewAssembler
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Reflection;
    using Assembler;
    using Glass;
    using Typing;

    public class StateCommuter
    {
        private readonly StackingLinkedList<Level> stack;
        private readonly WiringContext wiringContext;

        public StateCommuter(StackingLinkedList<Level> stack, WiringContext wiringContext)
        {
            this.stack = stack;
            this.wiringContext = wiringContext;
        }

        public void AssignChildToParentProperty()
        {
            var underlyingType = PreviousMember.Type.UnderlyingType;
            var compatibleValue = ConvertValueIfNecessary(Instance, underlyingType);

            PreviousValue.XamlMember.SetValue(PreviousInstance, compatibleValue);
        }

        public object ConvertValueIfNecessary(object value, Type targetType)
        {
            if (IsAlreadyCompatible(value, targetType))
            {
                return value;
            }

            object converted;
            var success = TrySpecialConversion(value, targetType, out converted);
            if (success)
            {
                return converted;
            }

            var typeConverter = wiringContext.ConverterProvider.GetTypeConverter(targetType);
            if (typeConverter != null)
            {
                var context = new XamlTypeConverterContext(wiringContext.TypeContext);
                var anotherValue = typeConverter.ConvertFrom(context, CultureInfo.InvariantCulture, value);
                return anotherValue;
            }

            throw new ValueConversionException($"Cannot convert {value} to type {targetType}");
        }

        private static bool IsAlreadyCompatible(object value, Type targetType)
        {
            return targetType.GetTypeInfo().IsAssignableFrom(value.GetType().GetTypeInfo());
        }

        private static bool TrySpecialConversion(object value, Type targetType, out object converted)
        {
            var type = value.GetType();

            if (type == typeof(string) && targetType == typeof(string))
            {
                converted = value;
                return true;
            }

            if (type == typeof(string) && targetType == typeof(object))
            {
                converted = value.ToString();
                return true;
            }

            if (targetType.GetTypeInfo().IsEnum)
            {
                converted = Enum.Parse(targetType, value.ToString());
                return true;
            }

            converted = null;
            return false;
        }

        public bool HasCurrentInstance => CurrentValue.Instance != null;

        public XamlType XamlType
        {
            get { return CurrentValue.XamlType; }
            set { CurrentValue.XamlType = value; }
        }

        public object Instance
        {
            get { return CurrentValue.Instance; }
            set { CurrentValue.Instance = value; }
        }

        private Level CurrentValue => stack.CurrentValue;

        private Level PreviousValue => stack.PreviousValue;

        public XamlMember Member
        {
            get { return CurrentValue.XamlMember; }
            set { CurrentValue.XamlMember = value; }
        }

        public int Level => stack.Count;

        public void RaiseLevel()
        {
            stack.Push(new Level());
        }

        public void DecreaseLevel()
        {
            stack.Pop();
        }

        public void CreateInstanceOfCurrentXamlTypeIfNotCreatedBefore()
        {
            if (!HasCurrentInstance)
            {
                MaterializeInstanceOfCurrentType();
            }
        }

        private void MaterializeInstanceOfCurrentType()
        {
            var xamlType = CurrentValue.XamlType;
            if (xamlType == null)
            {
                throw new InvalidOperationException("A type must be set before invoking MaterializeInstanceOfCurrentType");
            }
            var parameters = GatherConstructionArguments();
            var instance = xamlType.CreateInstance(parameters);
           
            CurrentValue.Instance = instance;
        }

        public object ReplaceInstanceByValueProvidedByMarkupExtension(MarkupExtension instance)
        {
            var markupExtensionContext = GetExtensionContext();
            return instance.ProvideValue(markupExtensionContext);
        }

        private MarkupExtensionContext GetExtensionContext()
        {
            var inflationContext = new MarkupExtensionContext
            {
                TargetObject = PreviousInstance,
                TargetProperty = PreviousInstance.GetType().GetRuntimeProperty(PreviousMember.Name),
                TypeRepository = wiringContext.TypeContext,
            };

            return inflationContext;
        }

        private object[] GatherConstructionArguments()
        {
            if (CtorArguments == null)
            {
                return null;
            }

            var arguments = CtorArguments.Select(argument => argument.Value).ToArray();
            return arguments.Any() ? arguments : null;
        }

        public bool IsGetObject
        {
            get { return CurrentValue.IsGetObject; }
            set { CurrentValue.IsGetObject = value; }
        }

        public ICollection Collection
        {
            get { return CurrentValue.Collection; }
            set { CurrentValue.Collection = value; }
        }

        public XamlMember PreviousMember => PreviousValue.XamlMember;
        public object PreviousInstance => PreviousValue.Instance;
        public bool PreviousIsHostingChildren => PreviousValue.Collection != null;

        public bool IsProcessingValuesAsCtorArguments => CurrentValue.IsProcessingValuesAsCtorArguments;
        public IList<ConstructionArgument> CtorArguments => CurrentValue.CtorArguments;

        public void AssignChildToCurrentCollection()
        {
            TypeOperations.Add(PreviousValue.Collection, Instance);
        }

        public void AddCtorArgument(string stringValue)
        {
            CurrentValue.CtorArguments.Add(new ConstructionArgument(stringValue));
        }

        public void BeginProcessingValuesAsCtorArguments()
        {
            CurrentValue.CtorArguments = new Collection<ConstructionArgument>();
            CurrentValue.IsProcessingValuesAsCtorArguments = true;
        }

        public void StopProcessingValuesAsCtorArguments()
        {
            CurrentValue.IsProcessingValuesAsCtorArguments = false;
        }

        public void ResetCtorArguments()
        {
            CurrentValue.CtorArguments = null;
        }

        public void AssociateCurrentInstanceToParent()
        {
            if (HasParentToAssociate && InstanceCanBeAssociated)
            {
                if (PreviousIsHostingChildren)
                {
                    AssignChildToCurrentCollection();
                }
                else
                {
                    AssignChildToParentProperty();
                }
            }
        }

        public bool InstanceCanBeAssociated => !(Instance is MarkupExtension);

        private bool HasParentToAssociate => Level > 1;
    }
}