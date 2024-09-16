# NestedText.NET
[NestedText](https://nestedtext.org/) library written in C#. WIP (50/144 official tests passing)

The library exposes the following static methods:
- `NestedTextSerializer.Format` 
- `NestedTextSerializer.Serialize` 
- `NestedTextSerializer.Deserialize`

These methods, apart from NestedText specific options (`NestedTextSerializerOptions`) also optionally take `JsonSerializerOptions`. This allows the library to given you access to the familiar API of the `System.Text.Json` namespace - its dozen of options, ability to write custom converters and so on.
This library implements an error-tolerant parser which does not ignore empty/comment lines. This powers the Format method which is able to format your NestedText source even when it contains errors and it does not remove your comments. For valid documents without comments, this is equivalent to roundtripping.
Just like `JsonSerializer`, this library does not just perform parsing & emiting, but also serializing & deserializing from a given schema (class). The (De)serialize methods are generic and work with any types for which there is a Converter defined. Default converters for booleans and numbers is provided.
