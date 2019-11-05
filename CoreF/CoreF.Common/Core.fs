namespace CoreF.Common

open System

[<AutoOpen>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Core =    
    /// Create a function that accepts any parameter and returns the given value
    let inline ret x = fun _ -> x

    /// Returns true if the given object is null, otherwise false
    let inline isNull x = (box x) |> isNull
    
    /// Returns true if the given object is not null, otherwise false
    let inline isNotNull x = x |> (isNull >> not)
    
    /// Executes the given function if the supplied object is not null
    let inline ifNotNull a f = if a |> isNotNull then f a
    
    /// Returns true if the given string is null, empty, or whitespace; otherwise returns false
    let inline isNullOrEmpty str = String.IsNullOrWhiteSpace str
    
    /// Returns true if the given string is not null and contains any non-whitespace character
    /// See also: `isNullOrEmpty`
    let inline isNotNullAndNotEmpty str = str |> (isNullOrEmpty >> not)

    /// Dereference a value from a reference cell or other container, e.g. Lazy<T>
    /// Works with any type that exposes a Value property with a public getter
    let inline (!) (cell: ^T when ^T : (member get_Value: unit -> 'a)) =
         ( ^T : (member get_Value : unit -> 'a) cell)

    /// Generic Parse - supports parsing to any type with a Parse method
    let inline parse< ^a when ^a : (static member Parse: string -> ^a) > str =
        (^a : (static member Parse: string -> ^a) str)

    /// Generic TryParse - supports parsing to any type with a TryParse method
    let inline tryParse< ^a when ^a : (static member TryParse: string * ^a byref -> bool) > str =
        let mutable cell = Unchecked.defaultof< ^a >
        if (^a : (static member TryParse: string * ^a byref -> bool) (str, &cell))
        then Some cell
        else None
    
module String =
    open System.Text.RegularExpressions

    /// Match the given string against the specified Regular Expression 
    let inline matches pattern text = Regex(pattern, RegexOptions.IgnoreCase).IsMatch(text)

    /// Test if the given string matches the supplied pattern
    /// Uses SQL-style matching by default, supporting '%' and '*' as wildcards
    /// Supports Regular-Expression matching if the `pattern` parameter is enclosed in '/' characters, e.g. "/This (.*) test/"
    let inline like (pattern: string) (str: string)  = // Example Usage:  "F# is Cool" |> like "f#*cool" 
        match pattern,str with
        | null,_ -> false
        | _,null -> false
        | regex,text when regex.StartsWith("/") -> text |> matches (regex.Replace("/", ""))            
        | wildcard,text when wildcard.Contains("*") || wildcard.Contains("%") -> 
            match wildcard.Split ([|'*'; '%'|], StringSplitOptions.RemoveEmptyEntries) with
            | [||] -> true
            | [|template|] -> if wildcard.StartsWith("*") || wildcard.StartsWith("%") then 
                                 if wildcard.EndsWith("*") || wildcard.EndsWith("%") then text.IndexOf(template, StringComparison.CurrentCultureIgnoreCase) >= 0
                                 else str.EndsWith(template, StringComparison.CurrentCultureIgnoreCase)
                              else str.StartsWith(template, StringComparison.CurrentCultureIgnoreCase)
            | pieces -> (pieces |> Array.fold (fun pos key -> if pos = -1 then -1 else let index = str.IndexOf(key, pos, StringComparison.InvariantCultureIgnoreCase)
                                                                                       if index < 0 then index
                                                                                       else index + key.Length) 0) = str.Length
        | _,_ -> str.Trim().Equals(pattern.Trim(), StringComparison.CurrentCultureIgnoreCase)

    /// Test if the given string does not match the supplied pattern
    /// See also: `like`
    let inline notLike pattern = like pattern >> not

    /// Convert the given string to lowercase
    let inline toLower str = if str |> isNotNullAndNotEmpty then str.ToLower() else str

    /// Convert the given string to uppercase
    let inline toUpper str = if str |> isNotNullAndNotEmpty then str.ToUpper() else str

    /// Replace all instances of the given substring in the string with the specified value
    let inline replace (find: string) (replace: string) (str: string) = 
        str.Replace(find, replace)

[<AutoOpen>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module StringPatterns =
    open System.Text.RegularExpressions

    /// Tests if the given string is like the pattern
    let (|Like|_|) (pattern: string) (str: string) =
        if str |> String.like pattern then Some Like else None

    /// Tests if the given string begins with the specified value
    let (|Begins|_|) (pattern: string) (str: string) =
        if str.StartsWith(pattern) then Some str else None

    /// Tests if the given string contains the specified value
    let (|Contains|_|) (pattern: string) (str: string) =
        if str.Contains(pattern) then Some str else None

    /// Tests if the given string ends with the specified value
    let (|Ends|_|) (pattern: string) (str: string) =
        if (str.EndsWith(pattern)) then Some str else None

    /// Tests if the given string matches the specified Regular Expression
    let (|Regex|_|) pattern str =
        let expression = new Regex(pattern)
        if expression.IsMatch(str) then Some str else None

    /// Tests if the given string maches the specified Regular Expression,
    /// and if so, returns the Regex Groups
    let (|RegexGroups|_|) pattern input =
        let m = Regex.Match(input, pattern)
        if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
        else None

// BOM-Free UTF-8 Encoding
module Utf8 =        
    let private UTF8NoBOM = Text.UTF8Encoding(false)

    /// Convert a string to a byte array to a BOM-free UTF8 string
    let toString = UTF8NoBOM.GetString

    /// Convert a string to a BOM-free UTF8 byte array
    let toBytes: string -> byte[] = UTF8NoBOM.GetBytes

    /// The BOM-free UTF8 encoding instance
    let encoding = UTF8NoBOM

// Base 64 Conversion
module Base64 =
    /// Convert a byte[] to a Base64 string
    let toString = Convert.ToBase64String

    /// Convert a Base64 string to a byte[]
    let toBytes = Convert.FromBase64String

    /// Try to parse a string as a Base64 encoded byte[]
    let tryParse (data: string) =
        try data |> toBytes |> Some
        with _ -> None

// URL helpers
module Url =
    type UrlFormatError =
    | InvalidRelativeUrl of Uri * string
    | UnexpectedErrorFormattingUrl of exn

    let tryParse url = 
        match Uri.TryCreate(url, UriKind.RelativeOrAbsolute) with
        | (true, uri) -> Some uri
        | _ -> None

    let makeRelative (fragment: string) baseUrl =
        match Uri.TryCreate(baseUrl, fragment) with
        | (true, uri) -> Ok uri
        | _ -> Error <| InvalidRelativeUrl (baseUrl, fragment)

    let tryMakeRelative fragment baseUrl =  
        match baseUrl |> makeRelative fragment with
        | Ok url -> Some url
        | Error _ -> None

    let makeRelativef formatFragment =
        Printf.ksprintf makeRelative formatFragment

    let tryMakeRelativef formatFragment =
        Printf.ksprintf tryMakeRelative formatFragment    