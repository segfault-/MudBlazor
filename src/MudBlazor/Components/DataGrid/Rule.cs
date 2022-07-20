// Copyright (c) MudBlazor 2021
// MudBlazor licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;

namespace MudBlazor
{
    public class Rule<T>
    {
        public Rule()
            : base()
        {
            Id = Guid.NewGuid();
            Rules = new();
        }

        public Rule(Rule<T> parent, string field)
            : this()
        {
            Parent = parent;
            Field = field;
        }

        public Rule<T> DeepClone()
        {
            return new Rule<T>()
            {
                Id = this.Id,
                Disabled = this.Disabled,
                Label = this.Label,
                Field = this.Field,
                Operator = this.Operator,
                Value = this.Value,
                Condition = this.Condition,

                Parent = this.Parent?.DeepClone(),

                Rules = new List<Rule<T>>(this.Rules.Select(r => r.DeepClone()).ToList()),
            };
        }

        public bool HasChild => Rules != null && Rules.Count > 0;

        public bool IsExpanded { get; set; } = true;
        public Guid Id { get; set; }
        public bool Disabled { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; }

        [JsonPropertyName("field")]
        public string Field { get; set; }

        [JsonPropertyName("operator")]
        public string Operator { get; set; }

        [JsonPropertyName("value")]
        public object Value { get; set; }

        [JsonPropertyName("condition")]
        public Condition? Condition { get; set; }

        public Rule<T> Parent { get; set; }
        [JsonPropertyName("rules")]
        public List<Rule<T>> Rules { get; set; }
    }
}
