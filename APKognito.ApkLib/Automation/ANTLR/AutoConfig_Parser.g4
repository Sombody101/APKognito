// $antlr-format alignTrailingComments true, columnLimit 150, minEmptyLines 1
// $antlr-format maxEmptyLinesToKeep 1, reflowComments false, useTab false
// $antlr-format allowShortRulesOnASingleLine false, allowShortBlocksOnASingleLine true
// $antlr-format alignSemicolons hanging, alignColons hanging

parser grammar AutoConfig_Parser;

options {
    tokenVocab = AutoConfig_Lexer;
    language = CSharp;
}

document
    : metadata? section* EOF
    ;

metadata
    : Meta LeftBrace (metaSetter)* RightBrace
    ;

section
    : sectionDeclaration line*
    ;

line
    : Identifier argument*
    ;

argument
    : StringConstant
    | Number
    | Word
    ;

sectionDeclaration
    : At Identifier
    ;

metaSetter
    : Identifier Colon argument
    ;