// Copyright (c) MudBlazor 2021
// MudBlazor licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MudBlazor.Utilities;
using static MudBlazor.CategoryTypes;
using static MudBlazor.Colors;

namespace MudBlazor
{

    public class ExpressionGenerator
    {
        private readonly MethodInfo _methodContains = typeof(Enumerable)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2);

        private delegate Expression Binder(Expression left, Expression right);

        [RequiresUnreferencedCode(CodeMessage.SerializationUnreferencedCodeMessage)]
        private Expression ParseTree<T>(Rule<T> condition, ParameterExpression parm)
        {
            Expression left = null;

            var binder = condition.Condition == Condition.AND ? (Binder)Expression.And : Expression.Or;

            Expression Bind(Expression left, Expression right) =>
                left == null ? right : binder(left, right);

            foreach (var rule in condition.Rules)
            {
                if (rule.Condition != null)
                {
                    var right = ParseTree<T>(rule, parm);
                    left = Bind(left, right);
                    continue;
                }

                var @operator = rule.Operator;
                var field = rule.Field;
                var property = Expression.Property(parm, field);

                if (@operator.Equals("is one of") || @operator.Equals("is not one of"))
                {
                    var jsonElement = (JsonElement)rule.Value;
                    var propertyType = typeof(T).GetProperty(rule.Field).PropertyType;
                    var contains = _methodContains.MakeGenericMethod(propertyType);

                    var val = jsonElement.ToString().Split(',').Select(v => v.Trim()).ToList();

                    var genericListType = typeof(List<>).MakeGenericType(propertyType);
                    var listy = (IList)Activator.CreateInstance(genericListType);

                    foreach (var s in val)
                    {
                        if (IsEnum(propertyType))
                        {
                            var nullableEnumType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
                            listy.Add(Enum.Parse(nullableEnumType, s));
                        }
                        else
                        {
                            listy.Add(s);
                        }

                    }

                    if (@operator.Equals("is not one of"))
                    {
                        var right = Expression.Not(Expression.Call(
                            contains,
                            Expression.Constant(listy),
                            property));
                        left = Bind(left, right);
                    }
                    else
                    {
                        var right = Expression.Call(
                            contains,
                            Expression.Constant(listy),
                            property);
                        left = Bind(left, right);
                    }
                }
                else
                {
                    Expression expression = null;
                    if (property.Type == typeof(string))
                    {
                        expression = GenerateFilterExpressionForStringType<T>(rule, property);
                    }
                    else if (IsEnum(property.Type))
                    {
                        expression = GenerateFilterExpressionForEnumType<T>(rule, property);
                    }
                    else if (IsNumber(property.Type))
                    {
                        expression = GenerateFilterExpressionForNumericType<T>(rule, property);
                    }
                    else if (IsBoolean(property.Type))
                    {
                        expression = GenerateFilterExpressionForBooleanType<T>(rule, property);
                    }
                    else if (IsDateTime(property.Type))
                    {
                        expression = GenerateFilterExpressionForDateTimeType<T>(rule, property);
                    }
                    else
                    {
                        throw new ArgumentException("Unhandled property type");
                    }



                    left = Bind(left, expression);
                }
            }

            return left;
        }

        private static Expression GenerateFilterExpressionForStringType<T>(Rule<T> rule, Expression parameter)
        {
            var dataType = typeof(T).GetProperty(rule.Field).PropertyType;
            var field = parameter;
            var valueString = rule.Value?.ToString();
            var trim = Expression.Call(field, dataType.GetMethod("Trim", Type.EmptyTypes));
            var isnull = Expression.Equal(field, Expression.Constant(null));
            var isnotnull = Expression.NotEqual(field, Expression.Constant(null));

            return rule.Operator switch
            {
                FilterOperator.String.Contains when rule.Value != null =>
                    Expression.AndAlso(isnotnull,
                        Expression.Call(field, dataType.GetMethod("Contains", new[] { dataType }), Expression.Constant(valueString))),

                FilterOperator.String.NotContains when rule.Value != null =>
                    Expression.AndAlso(isnotnull,
                        Expression.Not(Expression.Call(field, dataType.GetMethod("Contains", new[] { dataType }), Expression.Constant(valueString)))),

                FilterOperator.String.Equal when rule.Value != null =>
                    Expression.AndAlso(isnotnull,
                        Expression.Equal(field, Expression.Constant(valueString))),

                FilterOperator.String.NotEqual when rule.Value != null =>
                    Expression.AndAlso(isnotnull,
                    Expression.Not(Expression.Equal(field, Expression.Constant(valueString)))),

                FilterOperator.String.StartsWith when rule.Value != null =>
                    Expression.AndAlso(isnotnull,
                        Expression.Call(field, dataType.GetMethod("StartsWith", new[] { dataType }), Expression.Constant(valueString))),

                FilterOperator.String.EndsWith when rule.Value != null =>
                    Expression.AndAlso(isnotnull,
                        Expression.Call(field, dataType.GetMethod("EndsWith", new[] { dataType }), Expression.Constant(valueString))),

                FilterOperator.String.Empty =>
                    Expression.OrElse(isnull,
                        Expression.Equal(trim, Expression.Constant(string.Empty, dataType))),

                FilterOperator.String.NotEmpty =>
                    Expression.AndAlso(isnotnull,
                        Expression.NotEqual(trim, Expression.Constant(string.Empty, dataType))),

                _ => Expression.Constant(true, typeof(bool))
            };
        }

        private static Expression GenerateFilterExpressionForEnumType<T>(Rule<T> rule, Expression parameter)
        {
            var dataType = typeof(T).GetProperty(rule.Field).PropertyType;
            var field = parameter;
            var valueEnum = GetEnumFromObject(rule.Value, dataType);
            var @null = Expression.Convert(Expression.Constant(null), dataType);
            var isnull = Expression.Equal(field, @null);
            var isnotnull = Expression.NotEqual(field, @null);
            var valueEnumConstant = Expression.Convert(Expression.Constant(valueEnum), dataType);

            return rule.Operator switch
            {
                FilterOperator.Enum.Is when rule.Value != null =>
                    IsNullableEnum(dataType) ? Expression.AndAlso(isnotnull,
                            Expression.Equal(field, valueEnumConstant))
                        : Expression.Equal(field, valueEnumConstant),

                FilterOperator.Enum.IsNot when rule.Value != null =>
                    IsNullableEnum(dataType) ? Expression.OrElse(isnull,
                            Expression.NotEqual(field, valueEnumConstant))
                        : Expression.NotEqual(field, valueEnumConstant),

                _ => Expression.Constant(true, typeof(bool))
            };

        }

        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
        private static Expression GenerateFilterExpressionForNumericType<T>(Rule<T> rule, Expression parameter)
        {
            var dataType = typeof(T).GetProperty(rule.Field).PropertyType;
            var field = Expression.Convert(parameter, typeof(double?));
            var valueNumber = GetDoubleFromObject(rule.Value);
            var notNullNumber = Expression.Convert(field, typeof(double?));
            var valueNumberConstant = Expression.Constant(valueNumber, typeof(double?));


            return rule.Operator switch
            {
                FilterOperator.Number.Equal when rule.Value != null =>
                        Expression.Equal(notNullNumber, valueNumberConstant),

                FilterOperator.Number.NotEqual when rule.Value != null =>
                        Expression.NotEqual(notNullNumber, valueNumberConstant),

                FilterOperator.Number.GreaterThan when rule.Value != null =>
                        Expression.GreaterThan(notNullNumber, valueNumberConstant),

                FilterOperator.Number.GreaterThanOrEqual when rule.Value != null =>
                        Expression.GreaterThanOrEqual(notNullNumber, valueNumberConstant),

                FilterOperator.Number.LessThan when rule.Value != null =>
                        Expression.LessThan(notNullNumber, valueNumberConstant),

                FilterOperator.Number.LessThanOrEqual when rule.Value != null =>
                        Expression.LessThanOrEqual(notNullNumber, valueNumberConstant),

                FilterOperator.Number.Empty =>
                    Expression.Equal(field, Expression.Constant(null, typeof(double?))),

                FilterOperator.Number.NotEmpty =>
                    Expression.NotEqual(field, Expression.Constant(null, typeof(double?))),

                _ => Expression.Constant(true, typeof(bool))
            };
        }

        private static Expression GenerateFilterExpressionForDateTimeType<T>(Rule<T> rule, Expression parameter)
        {
            var dataType = typeof(T).GetProperty(rule.Field).PropertyType;
            if (dataType == typeof(DateTime))
            {
                var field = parameter;
                var valueDateTime = GetDateTimeFromObject(rule.Value);
                var notNullDateTime = Expression.Convert(field, typeof(DateTime));
                var valueDateTimeConstant = Expression.Constant(valueDateTime);

                return rule.Operator switch
                {
                    FilterOperator.DateTime.Is when null != rule.Value =>
                            Expression.Equal(notNullDateTime, valueDateTimeConstant),

                    FilterOperator.DateTime.IsNot when null != rule.Value =>
                            Expression.NotEqual(notNullDateTime, valueDateTimeConstant),

                    FilterOperator.DateTime.After when null != rule.Value =>
                            Expression.GreaterThan(notNullDateTime, valueDateTimeConstant),

                    FilterOperator.DateTime.OnOrAfter when null != rule.Value =>
                            Expression.GreaterThanOrEqual(notNullDateTime, valueDateTimeConstant),

                    FilterOperator.DateTime.Before when null != rule.Value =>
                            Expression.LessThan(notNullDateTime, valueDateTimeConstant),

                    FilterOperator.DateTime.OnOrBefore when null != rule.Value =>
                            Expression.LessThanOrEqual(notNullDateTime, valueDateTimeConstant),

                    _ => Expression.Constant(true, typeof(bool))
                };
            }
            else if (dataType == typeof(DateTime?))
            {
                var field = parameter;
                var valueDateTime = GetDateTimeFromObject(rule.Value);
                var notNullDateTime = Expression.Convert(field, typeof(DateTime?));
                var valueDateTimeConstant = Expression.Constant(valueDateTime, typeof(DateTime?));

                return rule.Operator switch
                {
                    FilterOperator.DateTime.Is when null != rule.Value =>
                        Expression.Equal(notNullDateTime, valueDateTimeConstant),

                    FilterOperator.DateTime.IsNot when null != rule.Value =>
                        Expression.NotEqual(notNullDateTime, valueDateTimeConstant),

                    FilterOperator.DateTime.After when null != rule.Value =>
                        Expression.GreaterThan(notNullDateTime, valueDateTimeConstant),

                    FilterOperator.DateTime.OnOrAfter when null != rule.Value =>
                        Expression.GreaterThanOrEqual(notNullDateTime, valueDateTimeConstant),

                    FilterOperator.DateTime.Before when null != rule.Value =>
                        Expression.LessThan(notNullDateTime, valueDateTimeConstant),

                    FilterOperator.DateTime.OnOrBefore when null != rule.Value =>
                        Expression.LessThanOrEqual(notNullDateTime, valueDateTimeConstant),

                    FilterOperator.DateTime.Empty =>
                        Expression.Equal(field, Expression.Constant(null, typeof(DateTime?))),

                    FilterOperator.DateTime.NotEmpty =>
                        Expression.NotEqual(field, Expression.Constant(null, typeof(DateTime?))),

                    _ => Expression.Constant(true, typeof(bool))
                };
            }
            else
            {
                throw new ArgumentException("uhandled dataType");
            }



        }

        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
        private static Expression GenerateFilterExpressionForBooleanType<T>(Rule<T> rule, Expression parameter)
        {
            var dataType = typeof(T).GetProperty(rule.Field).PropertyType;
            var field = Expression.Convert(parameter, typeof(bool?));
            var valueBool = GetBooleanFromObject(rule.Value);
            var notNullBool = Expression.Convert(field, typeof(bool?));

            return rule.Operator switch
            {
                FilterOperator.Enum.Is when rule.Value != null =>
                    Expression.Equal(notNullBool, Expression.Constant(valueBool, typeof(bool?))),

                _ => Expression.Constant(true, typeof(bool?))
            };
        }

        private static bool IsNullableEnum(Type t)
        {
            Type u = Nullable.GetUnderlyingType(t);
            return (u != null) && u.IsEnum;
        }

        public static List<string> GetListFromObject(object o)
        {
            if (o == null)
                return null;

            if (o is JsonElement element)
            {
                // return element.EnumerateArray().Select(e => e.GetString()).ToList();
                var x = element.ToString().Split(',').Select(v => v.Trim()).ToList();
                return x;
            }
            else
            {
                return null;

            }
        }

        public static DateTime? GetDateTimeFromObject(object o)
        {
            if (o == null)
                return null;

            if (o is JsonElement element)
            {
                return (DateTime?)Convert.ToDateTime(element.ToString()).ToUniversalTime();
            }
            else
            {
                return (DateTime?)Convert.ToDateTime(o).ToUniversalTime();
            }
        }

        public static bool? GetBooleanFromObject(object o)
        {
            if (o == null)
                return null;

            if (o is JsonElement element)
            {
                return (bool?)Convert.ToBoolean(element.ToString());
            }
            else
            {
                return (bool?)Convert.ToBoolean(o);
            }
        }

        public static Enum GetEnumFromObject(object o, Type t)
        {
            if (o == null)
                return null;

            var enumType = Nullable.GetUnderlyingType(t) ?? t;
            if (o is JsonElement element)
            {
                return (Enum)Enum.ToObject(enumType, element.GetInt32());
            }
            else if (enumType != null)
            {
                return (Enum)Enum.ToObject(enumType, o);
            }
            else
            {
                return (Enum)Enum.ToObject(t, o);
            }
        }

        public static double? GetDoubleFromObject(object o)
        {
            if (o == null)
                return null;

            if (o is JsonElement element)
            {
                return (double?)Convert.ToDouble(element.ToString());
            }
            else
            {
                return (double?)Convert.ToDouble(o);
            }
        }

        [RequiresUnreferencedCode(CodeMessage.SerializationUnreferencedCodeMessage)]
        public Expression<Func<T, bool>> ParseExpressionOf<T>(Rule<T> root)
        {
            Expression<Func<T, bool>> query = null;
            var itemExpression = Expression.Parameter(typeof(T));
            var conditions = ParseTree<T>(root, itemExpression);
            if (conditions != null)
            {
                if (conditions.CanReduce)
                {
                    conditions = conditions.ReduceAndCheck();
                }

                Console.WriteLine(conditions?.ToString());

                query = Expression.Lambda<Func<T, bool>>(conditions, itemExpression);
            }

            return query;
        }

        [RequiresUnreferencedCode(CodeMessage.SerializationUnreferencedCodeMessage)]
        public Func<T, bool> ParsePredicateOf<T>(Rule<T> root)
        {
            var query = ParseExpressionOf<T>(root);
            if (query != null)
            {
                return query.Compile();
            }
            else
            {
                return null;
            }

        }

        internal static bool IsEnum(Type type)
        {
            if (type == null)
                return false;

            if (type.IsEnum)
                return true;

            var u = Nullable.GetUnderlyingType(type);
            return (u != null) && u.IsEnum;
        }

        internal static bool IsNumber(Type type)
        {
            return NumericTypes.Contains(type);
        }

        internal static bool IsBoolean(Type type)
        {
            if (type == typeof(bool))
                return true;

            var u = Nullable.GetUnderlyingType(type);
            return (u != null) && u == typeof(bool);
        }

        internal static bool IsDateTime(Type type)
        {
            if (type == typeof(System.DateTime))
                return true;

            var u = Nullable.GetUnderlyingType(type);
            return (u != null) && u == typeof(System.DateTime);
        }

        internal static readonly HashSet<Type> NumericTypes = new()
        {
            typeof(int),
            typeof(double),
            typeof(decimal),
            typeof(long),
            typeof(short),
            typeof(sbyte),
            typeof(byte),
            typeof(ulong),
            typeof(ushort),
            typeof(uint),
            typeof(float),
            typeof(BigInteger),
            typeof(int?),
            typeof(double?),
            typeof(decimal?),
            typeof(long?),
            typeof(short?),
            typeof(sbyte?),
            typeof(byte?),
            typeof(ulong?),
            typeof(ushort?),
            typeof(uint?),
            typeof(float?),
            typeof(BigInteger?),
        };
    }

    public enum Condition
    {
        AND,
        OR
    }
}
