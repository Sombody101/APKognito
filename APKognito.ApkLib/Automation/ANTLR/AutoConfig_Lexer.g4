// $antlr-format alignTrailingComments true, columnLimit 150, minEmptyLines 1
// $antlr-format maxEmptyLinesToKeep 1, reflowComments false, useTab false
// $antlr-format allowShortRulesOnASingleLine false, allowShortBlocksOnASingleLine true
// $antlr-format alignSemicolons hanging, alignColons hanging

lexer
grammar AutoConfig_Lexer;

channels {
    ERROR
}

Semi
    : ';'
    ;

Colon
    : ':'
    ;

At
    : '@'
    ;

LeftBrace
    : '{'
    ;

RightBrace
    : '}'
    ;

Identifier
    : IdentifierStart (IdentifierPart* IdentifierLast)?
    ;

SingleLineComment
    : ';' ~[\r\n\u2028\u2029]* -> channel(HIDDEN)
    ;

Meta
    : At [mM][eE][tT][aA]
    ;

StringConstant
    : '"' WChar* '"'
    | '\'' LChar* '\''
    ;

Word
    : IdentifierStart [a-zA-Z0-9_-]*
    ;

Number
    : ('-'? IntStart IntPart | '-'? IntStart) ('.' (IntStart IntPart))?
    ;

Newline
    : ('\r' '\n'? | '\n') -> channel(HIDDEN)
    ;

Whitespace
    : [ \t\u000B\u000C\u00A0]+ -> channel(HIDDEN)
    ;

UnexpectedCharacter
    : . -> channel(ERROR)
    ;

fragment IntStart
    : [0-9]
    ;

fragment IntPart
    : [0-9_]+
    ;

fragment IdentifierStart
    : [a-zA-Z_]
    ;

fragment IdentifierPart
    : [a-zA-Z0-9_:]
    ;

fragment IdentifierLast
    : [a-zA-Z0-9_]
    ;

fragment WChar
    : ~["\r\n]
    ;

fragment LChar
    : ~['\r\n]
    ;