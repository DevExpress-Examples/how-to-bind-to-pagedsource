﻿Imports DevExpress.Data.Filtering
Imports DevExpress.Xpf.Data
Imports System
Imports System.ComponentModel
Imports System.Linq
Imports System.Windows

Namespace PagedSourceSample
    Partial Public Class MainWindow
        Inherits Window

        Public Sub New()
            InitializeComponent()

            Dim source = New PagedSource() With { _
                .ElementType = GetType(IssueData), _
                .PageNavigationMode = PageNavigationMode.ArbitraryWithTotalPageCount _
            }
            AddHandler Unloaded, Sub(o, e)
                source.Dispose()
            End Sub

            AddHandler source.CreateSource, Sub(o, e)
                e.Source = New IssuesContext()
            End Sub

            AddHandler source.DisposeSource, Sub(o, e)
                Dim issuesContext = CType(e.Source, IssuesContext)
                issuesContext.Dispose()
            End Sub

            AddHandler source.FetchPage, Sub(o, e)
                Dim fetchRowsResult = FetchRows(e)
                e.Result = fetchRowsResult.Rows
                e.HasMoreRows = fetchRowsResult.HasMoreRows
            End Sub

            AddHandler source.GetUniqueValues, Sub(o, e)
                If e.PropertyName = "Priority" Then
                    e.Result = System.Enum.GetValues(GetType(Priority)).Cast(Of Object)().ToArray()
                Else
                    Throw New InvalidOperationException()
                End If
            End Sub

            AddHandler source.GetTotalSummaries, Sub(o, e)
                e.Result = GetTotalSummaries(e)
            End Sub

            grid.ItemsSource = source
        End Sub
        Private Shared Function FetchRows(ByVal e As FetchPageEventArgs) As FetchRowsResult
            Dim sortOrder As IssueSortOrder = GetIssueSortOrder(e)
            Dim filter As IssueFilter = MakeIssueFilter(e.Filter)

            Dim issuesContext = CType(e.Source, IssuesContext)

            Const pageSize As Integer = 30
            Dim issues = issuesContext.GetIssues(page:= e.Skip / pageSize, pageSize:= pageSize, sortOrder:= sortOrder, filter:= filter)

            Return New FetchRowsResult(issues, hasMoreRows:= issues.Length = pageSize)
        End Function

        Private Shared Function GetTotalSummaries(ByVal e As GetSummariesEventArgs) As Object()
            Dim filter As IssueFilter = MakeIssueFilter(e.Filter)
            Dim issuesContext = CType(e.Source, IssuesContext)
            Dim summaryValues = issuesContext.GetSummaries(filter)
            Return e.Summaries.Select(Function(x)
                If x.SummaryType = SummaryType.Count Then
                    Return DirectCast(summaryValues.Count, Object)
                End If
                If x.SummaryType = SummaryType.Max AndAlso x.PropertyName = "Created" Then
                    Return summaryValues.LastCreated
                End If
                Throw New InvalidOperationException()
            End Function).ToArray()
        End Function

        Private Shared Function GetIssueSortOrder(ByVal e As FetchPageEventArgs) As IssueSortOrder
            If e.SortOrder.Length > 0 Then
                Dim sort = e.SortOrder.Single()
                If sort.PropertyName = "Created" Then
                    If sort.Direction <> ListSortDirection.Descending Then
                        Throw New InvalidOperationException()
                    End If
                    Return IssueSortOrder.CreatedDescending
                End If
                If sort.PropertyName = "Votes" Then
                    Return If(sort.Direction = ListSortDirection.Ascending, IssueSortOrder.VotesAscending, IssueSortOrder.VotesDescending)
                End If
            End If
            Return IssueSortOrder.Default
        End Function

        Private Shared Function MakeIssueFilter(ByVal filter As CriteriaOperator) As IssueFilter
            Return filter.Match(binary:= Function(propertyName, value, type)
                If propertyName = "Votes" AndAlso type = BinaryOperatorType.GreaterOrEqual Then
                    Return New IssueFilter(minVotes:= CInt((value)))
                End If
                If propertyName = "Priority" AndAlso type = BinaryOperatorType.Equal Then
                    Return New IssueFilter(priority:= CType(value, Priority))
                End If
                If propertyName = "Created" Then
                    If type = BinaryOperatorType.GreaterOrEqual Then
                        Return New IssueFilter(createdFrom:= CDate(value))
                    End If
                    If type = BinaryOperatorType.Less Then
                        Return New IssueFilter(createdTo:= CDate(value))
                    End If
                End If
                Throw New InvalidOperationException()
            End Function, [and]:= Function(filters)
                Return New IssueFilter(createdFrom:= filters.Select(Function(x) x.CreatedFrom).SingleOrDefault(Function(x) x IsNot Nothing), createdTo:= filters.Select(Function(x) x.CreatedTo).SingleOrDefault(Function(x) x IsNot Nothing), minVotes:= filters.Select(Function(x) x.MinVotes).SingleOrDefault(Function(x) x IsNot Nothing), priority:= filters.Select(Function(x) x.Priority).SingleOrDefault(Function(x) x IsNot Nothing))
            End Function, null:= Nothing)
        End Function
    End Class
End Namespace
