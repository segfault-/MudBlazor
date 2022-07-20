// Copyright (c) MudBlazor 2021
// MudBlazor licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor.Charts;
using static MudBlazor.Colors;

namespace MudBlazor
{
    public partial class RuleView<T> : MudComponentBase
    {
        [Parameter] public MudTreeViewItem<Rule<T>> Leaf { get; set; } = new();

        [CascadingParameter(Name = "Root")] public MudDataGrid<T> Root { get; set; }

        private Condition? ParentCondition
        {
            get
            {
                if (Leaf.Parent != null)
                {
                    return Leaf.Parent.Value.Condition;
                }
                else
                {
                    return Root.RootExpression.Condition;
                }
            }
        }

        private object _valueObject;
        private string _valueString;
        private double? _valueNumber;
        private Enum _valueEnum = null;
        private bool? _valueBool;
        private DateTime? _valueDate;
        private TimeSpan? _valueTime;
        private bool _isMultSelect;

        private string Field
        {
            get => Leaf.Value.Field;
            set
            {
                Type dtBefore = null;
                if (Leaf.Value.Field != null)
                {
                    dtBefore = typeof(T).GetProperty(Leaf.Value.Field).PropertyType;
                }
                Leaf.Value.Field = value;
                var dtAfter = typeof(T).GetProperty(Leaf.Value.Field).PropertyType;

                if (dtBefore != dtAfter)
                {
                    ClearValueStates();
                    Operator = null;
                    Value = null;
                }
            }
        }

        private string Operator
        {
            get => Leaf.Value.Operator;
            set
            {
                if (value != null)
                {
                    if (value.Equals("is one of") || value.Equals("is not one of"))
                    {
                        _isMultSelect = true;
                    }
                    else if (!string.IsNullOrEmpty(Operator))
                    {
                        if (Leaf.Value.Operator.Equals("is one of") || Leaf.Value.Operator.Equals("is not one of"))
                        {
                            ClearValueStates();
                            Value = null;
                        }

                        MultiSelectValues = new HashSet<string>();
                        _isMultSelect = false;
                    }
                }
                Leaf.Value.Operator = value;
            }
        }

        private object Value
        {
            get => Leaf.Value.Value;
            set
            {
                Leaf.Value.Value = value;
            }
        }


        private IEnumerable<string> MultiSelectValues { get; set; } = new HashSet<string>();

        protected bool HasChildren
        {
            get
            {
                var retVal = Leaf.Items.Any();
                return retVal;
            }
        }

        protected override Task OnParametersSetAsync()
        {
            if (Leaf.Value.Field == null)
            {
                return base.OnParametersSetAsync();
            }
            var dataType = typeof(T).GetProperty(Leaf.Value.Field).PropertyType;
            if (dataType == typeof(string))
            {
                if (Leaf.Value.Value is JsonElement element)
                {
                    _valueString = element.GetString();
                }
                else
                {
                    _valueString = Leaf.Value.Value?.ToString();
                }
            }
            else if (IsNumber)
            {
                if (Leaf.Value.Value is JsonElement element)
                {
                    _valueNumber = element.GetDouble();
                }
                else
                {
                    _valueNumber = Leaf.Value.Value == null ? null : Convert.ToDouble(Leaf.Value.Value);
                }
            }
            else if (IsEnum)
            {
                if (Leaf.Value.Value == null)
                {
                    _valueEnum = null;
                }
                else
                {
                    if (Leaf.Value.Value.GetType().IsEnum)
                    {
                        _isMultSelect = false;
                        _valueEnum = (Enum)Leaf.Value.Value;
                    }
                    else if (Leaf.Value.Value is string strVal)
                    {
                        _isMultSelect = true;
                        if (!string.IsNullOrWhiteSpace(strVal))
                        {
                            MultiSelectValues = strVal.Split(',').Select(v => v.Trim()).ToList();
                        }
                    }
                    else if (Leaf.Value.Value.GetType() == typeof(object))
                    {
                        _isMultSelect = true;
                        MultiSelectValues = (IEnumerable<string>)Leaf.Value.Value;
                    }
                    else
                    {
                        if (Leaf.Value.Operator.Equals("is one of") || Leaf.Value.Operator.Equals("is not one of"))
                        {
                            _isMultSelect = true;
                            if (Leaf.Value.Value is JsonElement element)
                            {
                                MultiSelectValues = element.ToString().Split(',').Select(v => v.Trim()).ToList();
                            }
                            else
                            {
                                MultiSelectValues = (IEnumerable<string>)Leaf.Value.Value;
                            }
                        }
                        else
                        {
                            var t = typeof(T).GetProperty(Leaf.Value.Field).PropertyType;
                            var tt = Nullable.GetUnderlyingType(t) ?? t;
                            var v = (Enum)Enum.ToObject(tt, ((JsonElement)Leaf.Value.Value).GetInt32());
                            _valueEnum = v;
                        }
                    }
                }
            }
            else if (dataType == typeof(bool) || dataType == typeof(bool?))
            {
                if (Leaf.Value.Value is JsonElement element)
                {
                    _valueBool = element.GetBoolean();
                }
                else
                {
                    _valueBool = Leaf.Value.Value == null ? null : Convert.ToBoolean(Leaf.Value.Value);
                }
            }
            else if (dataType == typeof(DateTime) || dataType == typeof(DateTime?))
            {
                if (Leaf.Value.Value is JsonElement element)
                {
                    var dateTime = element.GetDateTime();
                    _valueDate = Leaf.Value.Value == null ? null : dateTime;
                    _valueTime = Leaf.Value.Value == null ? null : dateTime.TimeOfDay;
                }
                else
                {
                    var dateTime = Convert.ToDateTime(Leaf.Value.Value);
                    _valueDate = Leaf.Value.Value == null ? null : dateTime;
                    _valueTime = Leaf.Value.Value == null ? null : dateTime.TimeOfDay;
                }
            }
            return base.OnParametersSetAsync();
        }

        protected void SetButtonText(int id)
        {
            switch (id)
            {
                case 0:
                    Leaf.Value.Condition = Condition.AND;
                    break;
                case 1:
                    Leaf.Value.Condition = Condition.OR;
                    break;
            }
            Leaf.CallStateHasChanged();
        }

        protected void ClearValueStates()
        {
            _valueObject = null;
            _valueString = null;
            _valueNumber = null;
            _valueEnum = null;
            _valueBool = null;
            _valueDate = null;
            _valueTime = null;
            _isMultSelect = false;
        }

        protected void AddExpressionRule()
        {
            if (!Leaf.Items.Any())
            {
                SetLeafCondition();
            }

            Leaf.Items.Add(CloneRule());
            Leaf.Value.Field = null;
            Leaf.Value.Operator = null;
            Leaf.Value.Value = null;
            Leaf.Value.IsExpanded = true;
            Leaf.Value.Disabled = false;
            Leaf.CallStateHasChanged();
        }

        private void SetLeafCondition()
        {
            Condition? condition = null;
            if (Leaf.Parent != null)
            {
                condition = Leaf.Parent.Value.Condition;
            }
            else if (Root != null)
            {
                condition = Root.RootExpression.Condition;
            }

            if (condition != null)
            {
                if (condition == Condition.AND)
                {
                    condition = Condition.OR;
                }
                else
                {
                    condition = Condition.AND;
                }
            }
            else
            {
                condition = Condition.AND;
            }

            Leaf.Value.Condition = condition;
        }

        private Rule<T> CloneRule()
        {
            if (MultiSelectValues.Any())
            {
                var rr = new Rule<T>(null, Leaf.Value.Field)
                {
                    Operator = Leaf.Value.Operator,
                    Value = MultiSelectValues,
                };
                return rr;
            }

            var r = new Rule<T>(null, Leaf.Value.Field)
            {
                Operator = Leaf.Value.Operator,
                Value = Leaf.Value.Value,

            };
            return r;
        }


        protected void RemoveExpressionRule()
        {
            Root?.RemoveRule(Leaf.Value);
            Leaf.Parent?.Items.Remove(Leaf.Value);
        }

        internal void StringValueChanged(string value)
        {
            Leaf.Value.Value = value;
            _valueString = value;
        }

        internal void NumberValueChanged(double? value)
        {
            Leaf.Value.Value = value;
            _valueNumber = value;
        }

        internal void EnumValueChanged(Enum value)
        {
            Leaf.Value.Value = value;
            _valueEnum = value;
        }

        internal void ObjectValueChanged(object value)
        {
            Leaf.Value.Value = value;
            _valueObject = value;
        }

        internal void BoolValueChanged(bool? value)
        {
            Leaf.Value.Value = value;
            _valueBool = value;
        }

        internal void DateValueChanged(DateTime? value)
        {
            _valueDate = value;

            if (value != null)
            {
                var date = value.Value.Date;

                // get the time component and add it to the date.
                if (_valueTime != null)
                {
                    date = date.Add(_valueTime.Value);
                }
                Leaf.Value.Value = date;
            }
            else
            {
                Leaf.Value.Value = value;
            }

            //DataGrid.GroupItems();
        }

        internal void TimeValueChanged(TimeSpan? value)
        {
            _valueTime = value;

            if (_valueDate != null)
            {
                var date = _valueDate.Value.Date;

                // get the time component and add it to the date.
                if (_valueTime != null)
                {
                    date = date.Add(_valueTime.Value);
                }

                Leaf.Value.Value = date;
            }

            //DataGrid.GroupItems();
        }
        private bool IsString
        {
            get
            {
                return FilterOperator.IsString(typeof(T).GetProperty(Leaf.Value.Field).PropertyType);
            }
        }
        private bool IsNumber
        {
            get
            {
                return FilterOperator.IsNumber(typeof(T).GetProperty(Leaf.Value.Field).PropertyType);
            }
        }
        private bool IsEnum
        {
            get
            {
                return FilterOperator.IsEnum(typeof(T).GetProperty(Leaf.Value.Field).PropertyType);
            }
        }

        private static Array GetEnumValues(Type t)
        {
            if (t == null)
            {
                return Array.Empty<int>();
            }

            var nullableEnumType = Nullable.GetUnderlyingType(t);
            if (nullableEnumType != null)
            {
                return nullableEnumType.GetEnumValues();
            }
            else
            {
                return t.GetEnumValues();
            }
        }
    }
}
