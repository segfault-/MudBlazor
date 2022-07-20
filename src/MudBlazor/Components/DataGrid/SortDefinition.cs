// Copyright (c) MudBlazor 2021
// MudBlazor licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Serialization;

namespace MudBlazor
{
    public sealed record SortDefinition<T>(
        [property: JsonPropertyName("SortBy")] string SortBy,
        [property: JsonPropertyName("Descending")] bool Descending,
        [property: JsonPropertyName("Index")] int Index,
        [property: JsonIgnore] Func<T, object> SortFunc);
}
