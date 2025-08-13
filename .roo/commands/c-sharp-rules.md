---
description: "c# features.  use these features when applicable"
---

C# 14 —

The field keyword: — Previously, if you wanted to ensure that a string property couldn’t be set to null, you had to declare a backing field and implement both accessors:
private string \_msg;
public string Message
{
get => \_msg;
set => \_msg = value ?? throw new ArgumentNullException(nameof(value));
}
in C# 14

public string Message
{
get;
set => field = value ?? throw new ArgumentNullException(nameof(value));
}
Implicit span conversions: — An implicit span conversion permits array_types, System.Span<T>, System.ReadOnlySpan<T>, and string to be converted between each other as follows:

1. From any single-dimensional array_type with element type Ei to System.Span<Ei>

C# 13 —

LINQ Index
(The Index() method (Enumerable.Index) adds an index to each element in the collection. The index is a 0-based integer that represents the position of the element in the collection.)

IEnumerable<(int, Employee)> employeesWithIndex = employees.Index();

foreach (var (index, emp) in employeesWithIndex)
{
Console.WriteLine($"Employee {index}: {emp.Name}");
}
CountBy, AggregateBy
The CountBy() method (Enumerable.CountBy<TSource, TKey>) groups the elements in the collection by a key and returns the count of elements in each group.

The AggregateBy() method (Enumerable.AggregateBy<TSource, TKey, TAccumulate>) groups the elements in the collection by a key and aggregates (similar to Aggregate()) the elements in each group.

Params collection
The params keyword in C# allows a method to accept the variable number of arguments, which must be a single-dimensional array. This parameter must be the last in the method signature, and only one params parameter is allowed per method.

UUID version 7
A Version 7 UUID is a universally unique identifier that is generated using a timestamp, a counter and a cryptographically strong random number.

Lock
The .NET runtime includes a new type for thread synchronization, the System.Threading.Lock type. This type provides better thread synchronization through its API. The Lock.EnterScope() method enters an exclusive scope. The ref struct returned from that supports the Dispose() pattern to exit the exclusive scope

4. C# 12 —

Primary constructors
This feature aims to simplify the initialization of properties in classes, especially for classes with many properties. This article will explore Primary Constructors and see how they work.

Collection expressions
Inline collections with ranges and slices
Default values for lambda expressions
C# 11

Raw literal strings
Allow a new form of string literal that starts with a minimum of three """ characters (but no maximum), optionally followed by a new_line, the content of the string, and then ends with the same number of quotes that the literal started with. For example:

var xml = """
<element attr="content"/>
""";
List patterns
File-scoped types
Required members
C# 10

File scoped namespace
File scoped namespaces use a less verbose format for the typical case of files containing only one namespace. The file scoped namespace format is namespace X.Y.Z; (note the semicolon and lack of braces). This allows for files like the following:

namespace X.Y.Z;

using System;

class X
{
}
Global using directive
Constant interpolated strings
C# 9

Records
In C#, records are a type of reference or value type (introduced in C# 9 and 10 respectively) designed for data-centric scenarios, offering a concise syntax for creating immutable data models with built-in value equality and display capabilities.

Init-only setters
Top-level statements
Improved pattern matching
Target-type new
