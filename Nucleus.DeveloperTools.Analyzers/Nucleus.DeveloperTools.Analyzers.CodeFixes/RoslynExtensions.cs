using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;

namespace Nucleus.DeveloperTools.Analyzers
{
  internal static class RoslynExtensions
  {
    public static ClassDeclarationSyntax AddAttribute(this ClassDeclarationSyntax classDeclaration, string attributeName, string[] args)
    {
      SyntaxList<AttributeListSyntax> attributes = classDeclaration.AttributeLists.Add
      (
        CreateSingleAttribute(attributeName, args)
          .NormalizeWhitespace()
          .WithLeadingWhitespaceFrom(classDeclaration)
          .WithTrailingTrivia(SyntaxFactory.Whitespace("\n"))
      );

      return classDeclaration.WithAttributeLists(attributes);
        //.WithTriviaFrom(classDeclaration);
    }

    public static MethodDeclarationSyntax AddAttribute(this MethodDeclarationSyntax methodDeclaration, string attributeName, string[] args)
    {
      SyntaxList<AttributeListSyntax> attributes = methodDeclaration.AttributeLists.Add
      (
        CreateSingleAttribute(attributeName, args)
          .NormalizeWhitespace()
          .WithLeadingWhitespaceFrom(methodDeclaration)
          .WithTrailingTrivia(SyntaxFactory.Whitespace("\n"))
      );

      return methodDeclaration.WithAttributeLists(attributes);
        //.WithTriviaFrom(methodDeclaration);      
    }

    public static TSyntax WithLeadingWhitespaceFrom<TSyntax>(this TSyntax syntax, SyntaxNode node) where TSyntax : SyntaxNode
    {
      return syntax.WithLeadingTrivia(node.GetLeadingTrivia().GetWhiteSpace().FirstOrDefault());
    }

    private static SyntaxTriviaList GetWhiteSpace(this SyntaxTriviaList value)
    {
      return value
        .Where(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia))
        .ToSyntaxTriviaList();
    }


    private static SyntaxTriviaList GetWhiteSpac2e(SyntaxNode declaration)
    {
      return declaration.GetLeadingTrivia()
        .Where(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia))
        .ToSyntaxTriviaList();      
    }

    private static AttributeListSyntax CreateSingleAttribute(string attributeName, string[] args)
    {
      SeparatedSyntaxList<AttributeArgumentSyntax> attributeArguments = SyntaxFactory.SingletonSeparatedList<AttributeArgumentSyntax>(null);

      foreach (string arg in args)
      {
        string[] argParts = arg.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

        if (argParts.Length == 1)
        {
          // Add an un-named attribute argument (value)

          // Note: attributeArguments (SeparatedSyntaxList<AttributeArgumentSyntax>) is immutable.  The .Add method
          // returns a new instance with the argument added, which must be assigned to attributeArguments.
          attributeArguments = attributeArguments.Add
          (
            SyntaxFactory.AttributeArgument
            (
              SyntaxFactory.IdentifierName(argParts[0])
            )
          );
        }
        else if (argParts.Length == 2)
        {
          // Add a named attribute argument (name=value)

          // Note: attributeArguments (SeparatedSyntaxList<AttributeArgumentSyntax>) is immutable.  The .Add method
          // returns a new instance with the argument added, which must be assigned to attributeArguments.
          attributeArguments = attributeArguments.Add
          (
            SyntaxFactory.AttributeArgument
            (
              SyntaxFactory.IdentifierName(argParts[1])
            )
            .WithNameEquals(SyntaxFactory.NameEquals(SyntaxFactory.IdentifierName(argParts[0])))
          );
        }
      }

      return SyntaxFactory.AttributeList
      (
        SyntaxFactory.SingletonSeparatedList<AttributeSyntax>
        (
          SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(attributeName))
            .WithArgumentList(SyntaxFactory.AttributeArgumentList(attributeArguments))
        )
      );
    }
  }
}
