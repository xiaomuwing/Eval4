Public Module Globals
    Friend Function varEq(ByVal v1 As String, ByVal v2 As String) As Boolean
        Dim lv1, lv2 As Integer
        If v1 Is Nothing Then lv1 = 0 Else lv1 = v1.Length
        If v2 Is Nothing Then lv2 = 0 Else lv2 = v2.Length

        If lv1 <> lv2 Then Return False
        If lv1 = 0 Then Return True

        Dim c1, c2 As Char

        For i As Integer = 0 To lv1 - 1
            c1 = v1.Chars(i)
            c2 = v2.Chars(i)
            Select Case c1
                Case "a"c To "z"c
                    If c2 <> c1 AndAlso Asc(c2) <> (Asc(c1) - 32) Then
                        Return False
                    End If
                Case "A"c To "Z"c
                    If c2 <> c1 AndAlso Asc(c2) <> (Asc(c1) + 32) Then
                        Return False
                    End If
                Case "-"c, "_"c, "."c
                    If c2 <> c1 AndAlso c2 <> "_"c AndAlso c2 <> "."c Then
                        Return False
                    End If
                Case "_"c
                    If c2 <> c1 AndAlso c2 <> "-"c Then
                        Return False
                    End If
                Case Else
                    If c2 <> c1 Then Return False
            End Select
        Next
        Return True
    End Function

    Friend Function GetObjectType(ByVal o As Object) As EvalType
        If o Is Nothing Then
            Return EvalType.Unknown
        Else
            Dim t As Type = o.GetType
            Return GetEvalType(t)
        End If
    End Function

    Friend Function GetEvalType(ByVal t As Type) As EvalType
        If t Is GetType(Single) _
                Or t Is GetType(Double) _
                Or t Is GetType(Decimal) _
                Or t Is GetType(Int16) _
                Or t Is GetType(Int32) _
                Or t Is GetType(Int64) _
                Or t Is GetType(Byte) _
                Or t Is GetType(UInt16) _
                Or t Is GetType(UInt32) _
                Or t Is GetType(UInt64) _
                Then
            Return EvalType.Number
        ElseIf t Is GetType(Date) Then
            Return EvalType.Date
        ElseIf t Is GetType(Boolean) Then
            Return EvalType.Boolean
        ElseIf t Is GetType(String) Then
            Return EvalType.String
        Else
            Return EvalType.Object
        End If
    End Function

    Friend Function GetSystemType(ByVal t As EvalType) As Type
        Select Case t
            Case EvalType.Boolean
                Return GetType(Boolean)
            Case EvalType.Date
                Return GetType(Date)
            Case EvalType.Number
                Return GetType(Double)
            Case EvalType.String
                Return GetType(String)
            Case Else
                Return GetType(Object)
        End Select
    End Function

    Public Function TBool(ByVal o As iEvalTypedValue) As Boolean
        Return CBool(o.Value)
    End Function

    Public Function TDate(ByVal o As iEvalTypedValue) As Date
        Return CDate(o.Value)
    End Function

    Public Function TNum(ByVal o As iEvalTypedValue) As Double
        Return CDbl(o.Value)
    End Function

    Public Function TStr(ByVal o As iEvalTypedValue) As String
        Return CStr(o.Value)
    End Function

End Module

Friend Enum ePriority
    none = 0
    [or] = 1
    [and] = 2
    [not] = 3
    equality = 4
    [concat] = 5
    plusminus = 6
    muldiv = 7
    percent = 8
    unaryminus = 9
End Enum

Public Enum EvalType
    Unknown
    Number
    [Boolean]
    [String]
    [Date]
    [Object]
End Enum

Public Enum eParserSyntax
    cSharp
    Vb
End Enum

Public Enum eTokenType
    none
    end_of_formula
    operator_plus
    operator_minus
    operator_mul
    operator_div
    operator_percent
    open_parenthesis
    comma
    dot
    close_parenthesis
    operator_ne
    operator_gt
    operator_ge
    operator_eq
    operator_le
    operator_lt
    operator_and
    operator_or
    operator_not
    operator_concat
    operator_if

    value_identifier
    value_true
    value_false
    value_number
    value_string
    value_date
    open_bracket
    close_bracket

End Enum
