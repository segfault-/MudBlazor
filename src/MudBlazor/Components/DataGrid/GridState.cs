// Copyright (c) MudBlazor 2021
// MudBlazor licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MudBlazor
{
    public class GridState<T>
    {
        [JsonPropertyName("Page")] public int Page { get; set; }

        [JsonPropertyName("PageSize")] public int PageSize { get; set; }

        [JsonPropertyName("SortDefinitions")] public ICollection<SortDefinition<T>> SortDefinitions { get; set; }

        [JsonPropertyName("RootExpression")] public Rule<T> RootExpression { get; set; }
    }

    public class GridData<T>
    {
        public IEnumerable<T> Items { get; set; }
        public int TotalItems { get; set; }
    }
}
