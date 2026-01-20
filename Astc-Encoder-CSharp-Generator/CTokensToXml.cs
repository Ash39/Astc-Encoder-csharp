using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace Astc_Encoder_CSharp_Generator
{
    internal class CTokensToXml
    {
        public static void ConvertTokensToXml(AstcSourceDownload sourceDownload, string[] preprocessors, string MethodExportTokenName)
        {
            List<Token> tokens = Tokenizer.Tokenize(sourceDownload.Code);

            XmlDocument document = new XmlDocument();

            document.AppendChild(document.CreateXmlDeclaration("1.0", "utf-8", null));

            XmlElement root = document.CreateElement("Header");

            document.AppendChild(root);

            List<Token> preprocessedTokens = PreprocesserParse(tokens, preprocessors);

            try
            {
                for (int i = 0; i < preprocessedTokens.Count; i++)
                {
                    Token token = preprocessedTokens[i];

                    string location = string.Empty;
                    Token commentToken = null;

                    switch (token.Kind)
                    {
                        case TokenType.Identifier:
                            if (token.Value == MethodExportTokenName)
                            {
                                MethodParse(document, root, preprocessedTokens, ref i);
                            }
                            break;
                        case TokenType.Keyword:
                            switch (token.Value)
                            {
                                case "struct":
                                    location = StructParse(document, root, preprocessedTokens, ref i);
                                    break;
                                case "static":
                                    location = StaticParse(document, root, preprocessedTokens, ref i);
                                    break;
                                case "typedef":
                                    location = FunctionPointerParse(document, root, preprocessedTokens, ref i);
                                    break;
                                case "enum":
                                    location = EnumParse(document, root, preprocessedTokens, ref i);
                                    break;
                            }
                            break;
                        case TokenType.Comment:
                            commentToken = token;
                            break;
                    }

                    if (commentToken != null) 
                    {
                        ParseComment(location, document, root, commentToken);
                        commentToken = null;
                    }
                }
            }
            catch (Exception)
            { 
            }

            StringWriter sw = new StringWriter();
            document.WriteTo(new XmlTextWriter(sw));

            sourceDownload.Xml = sw.ToString();
        }

        private static List<Token> PreprocesserParse(List<Token> tokens, string[] preprocessors)
        {
            List<Token> tempTokens = new List<Token>();

            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];

                List<TokenWord> words = token.Tokens;

                for (int j = 0; j < words.Count; j++)
                {
                    TokenWord word = words[j];

                    if (word.Value == "defined")
                    {
                        if (preprocessors.Contains(words[j + 1].Value))
                        {
                            while (tokens[++i].Kind != TokenType.Preprocessor)
                            {
                                tempTokens.Add(tokens[i]);
                            }
                        }
                        while (tokens[++i].Kind != TokenType.Preprocessor)
                        {
                        }
                    }
                }

                if (token.Value == "#else")
                {
                    while (tokens[++i].Kind != TokenType.Preprocessor)
                    {
                        tempTokens.Add(tokens[i]);
                    }
                }
                tempTokens.Add(token);
            }

            return tempTokens;
        }

        private static void ParseComment(string location,XmlDocument document, XmlElement parent, Token token)
        {
            XmlElement commentElement = document.CreateElement("Comment");
            commentElement.SetAttribute("Location", location);
            commentElement.InnerText = token.Value;
            parent.AppendChild(commentElement);
        }

        private static string StructParse(XmlDocument document, XmlElement root, List<Token> tokens, ref int i)
        {
            Token structName = tokens[++i];
            XmlElement tokenElement = document.CreateElement("Struct");
            tokenElement.SetAttribute("Name", structName.Value);

            Token advancedToken = tokens[++i];

            bool hasOpenBrace = false;

            Token commentToken = null;

            while (advancedToken.Value != "}")
            {
                if (!hasOpenBrace)
                    hasOpenBrace = advancedToken.Value == "{";

                if (advancedToken.Value == ";" && !hasOpenBrace)
                    break;

                if (hasOpenBrace)
                {
                    if (advancedToken.Kind == TokenType.Comment)
                    {
                        commentToken = advancedToken;
                    }
                    else if (advancedToken.Kind == TokenType.Identifier)
                    {
                        XmlElement fieldElement = document.CreateElement("Field");

                        Token prefixToken = advancedToken;
                        string prefix = string.Empty;

                        if (ParseSpecifier(prefixToken.Value))
                        {
                            while (ParseSpecifier(prefixToken.Value))
                            {
                                prefix += prefixToken.Value + " ";
                                prefixToken = tokens[++i];
                            }
                            advancedToken = prefixToken;
                        }

                        string typeValue = advancedToken.Value;

                        Token fieldNameToken = tokens[++i];

                        while (fieldNameToken.Value == "*")
                        {
                            typeValue += tokens[i].Value;
                            fieldNameToken = tokens[++i];
                        }

                        fieldElement.SetAttribute("Prefix", prefix.Trim());
                        fieldElement.SetAttribute("Type", typeValue);
                        fieldElement.SetAttribute("Name", fieldNameToken.Value);

                        string suffex = string.Empty;

                        while (tokens[++i].Value != ";")
                        {
                            suffex += tokens[i].Value;
                        }

                        fieldElement.SetAttribute("Suffix", suffex);

                        tokenElement.AppendChild(fieldElement);

                        if (commentToken != null) 
                        {
                            ParseComment(structName.Value + "." + fieldNameToken.Value, document, tokenElement, commentToken);
                            commentToken = null;
                        }
                            
                    }
                }

                advancedToken = tokens[++i];
            }

            root.AppendChild(tokenElement);

            return structName.Value;
        }

        private static string StaticParse(XmlDocument document, XmlElement root, List<Token> tokens, ref int i)
        {
            XmlElement tokenElement = document.CreateElement("StaticField");

            Token advancedToken = tokens[i+=2];
            List<string> fieldBody = new List<string>();

            while (advancedToken.Value != ";")
            {
                if (advancedToken.Value == "=")
                {
                    string initializer = string.Empty;
                    while (tokens[++i].Value != ";")
                    {
                        initializer += tokens[i].Value;
                    }
                    fieldBody.Add(initializer);
                    advancedToken = tokens[i];
                    continue;
                }
                    
                fieldBody.Add(advancedToken.Value);

                advancedToken = tokens[++i];
            }

            bool hasModifier = fieldBody.Count == 4;

            int typeIndex = hasModifier ? 0 : -1;

            tokenElement.SetAttribute(hasModifier ? "Modifier" : "Type", fieldBody[0]);

            if (hasModifier)
                tokenElement.SetAttribute("Type", fieldBody[1]);

            tokenElement.SetAttribute("Name", fieldBody[typeIndex + 2]);
            tokenElement.SetAttribute("Initializer", fieldBody[typeIndex + 3]);

            root.AppendChild(tokenElement);

            return fieldBody[typeIndex + 2];
        }

        private static string EnumParse(XmlDocument document, XmlElement root, List<Token> tokens, ref int i)
        {
            Token enumName = tokens[++i];
            XmlElement tokenElement = document.CreateElement("Enum");
            tokenElement.SetAttribute("Name", enumName.Value);

            Token advancedToken = tokens[++i];
            bool enumEnded = false;

            Token commentToken = null;

            while (advancedToken.Value != "}")
            {
                if (advancedToken.Kind == TokenType.Comment)
                {
                    commentToken = advancedToken;
                }
                else if (advancedToken.Kind == TokenType.Identifier)
                {
                    XmlElement fieldElement = document.CreateElement("Constant");

                    Token nameToken = advancedToken;


                    Token valueToken = tokens[tokens[i + 1].Value == "=" ? i += 2 : ++i];

                    string value = string.Empty;

                    while (valueToken.Value != ",")
                    {
                        if (valueToken.Value == "}")
                        {
                            advancedToken = valueToken;
                            enumEnded = true;
                            break;
                        }
                        value += valueToken.Value;
                        valueToken = tokens[++i];
                    }

                    fieldElement.SetAttribute("Name", nameToken.Value);
                    fieldElement.SetAttribute("Value", value);

                    tokenElement.AppendChild(fieldElement);

                    if(commentToken != null)
                        ParseComment(enumName.Value + "." + nameToken.Value, document, tokenElement, commentToken);
                    commentToken = null;
                }
                if (!enumEnded)
                    advancedToken = tokens[++i];
            }

            root.AppendChild(tokenElement);

            return enumName.Value;
        }

        private static void MethodParse(XmlDocument document, XmlElement root, List<Token> tokens, ref int i)
        {
            XmlElement tokenElement = document.CreateElement("Method");

            List<Token> temp = new List<Token>();

            while (tokens[++i].Value != "(")
            {
                temp.Add(tokens[i]);
            }

            tokenElement.SetAttribute("Name", temp.Last().Value);

            string returnSpecifier = string.Empty;

            Token returnType = temp[0];

            int j = 0;

            if (ParseSpecifier(returnType.Value))
            {
                newSpecifier:
                {
                    returnSpecifier += returnType.Value + " ";
                    returnType = temp[++j];
                }

                if (ParseSpecifier(returnType.Value))
                    goto newSpecifier;
            }

            string returnTypeValue = returnType.Value;

            for (int k = 0; k < temp.Count; k++)
            {
                if (returnType == temp[k])
                {
                    while (temp[k + 1].Value == "*")
                    {
                        returnTypeValue += temp[++k].Value;
                    }
                }
            }

            tokenElement.SetAttribute("Return", returnTypeValue);
            tokenElement.SetAttribute("ReturnTypePrefix", returnSpecifier.Trim());


            List<string> paramParts = new List<string>();

            Token advancedToken = tokens[++i];

            while (advancedToken.Value != ")")
            {
                if (advancedToken.Value == "(")
                {
                    advancedToken = tokens[++i];
                    continue;
                }

                string typeValue = advancedToken.Value;

                advancedToken = tokens[++i];

                while (advancedToken.Value == "*")
                {
                    typeValue += tokens[i].Value;
                    advancedToken = tokens[++i];
                }

                if (advancedToken.Value == "[")
                {
                    while (true)
                    {
                        typeValue += tokens[i].Value;
                        advancedToken = tokens[++i];

                        if (advancedToken.Value == "]")
                        {
                            typeValue += advancedToken.Value;
                            advancedToken = tokens[++i];
                            break;
                        }

                    }
                }


                paramParts.Add(typeValue);

                if (advancedToken.Value == "," || advancedToken.Value == ")")
                {
                    string[] nameSuffixSpilt = paramParts.Last().Split('[');

                    XmlElement paramElement = document.CreateElement("Param");
                    paramElement.SetAttribute("Name", nameSuffixSpilt[0]);
                    paramElement.SetAttribute("Suffix", paramParts.Last().Replace(nameSuffixSpilt[0],string.Empty));

                    bool hasModifier = paramParts.Count == 3;

                    paramElement.SetAttribute(hasModifier ? "Prefix" : "Type", paramParts[0]);

                    if (hasModifier)
                        paramElement.SetAttribute("Type", paramParts[1]);

                    tokenElement.AppendChild(paramElement);
                    paramParts.Clear();

                    if (advancedToken.Value == ")")
                        break;

                    advancedToken = tokens[++i];
                }
            }

            root.AppendChild(tokenElement);

        }

        private static string FunctionPointerParse(XmlDocument document, XmlElement root, List<Token> tokens, ref int i)
        {
            Token returnType = tokens[++i];
            XmlElement tokenElement = document.CreateElement("FunctionPointer");

            string returnSpecifier = string.Empty;

            if (ParseSpecifier(returnType.Value))
            {
                newSpecifier:
                {
                    returnSpecifier += returnType.Value + " ";
                    returnType = tokens[++i];
                }
                    
                if (ParseSpecifier(returnType.Value))
                    goto newSpecifier;
            }

            tokenElement.SetAttribute("Return", returnType.Value);
            tokenElement.SetAttribute("ReturnTypePrefix", returnSpecifier.Trim());

            string functionName = string.Empty;
            while (tokens[++i].Value != ";")
            {
                Token token = tokens[i];
                Token perviousToken = tokens[i - 1];

                if (token.Kind == TokenType.Identifier && perviousToken.Value == "*")
                {
                    functionName = token.Value;
                }
                else if (token.Kind == TokenType.Identifier)
                {
                    string type = string.Empty;
                    string prefix = string.Empty;

                    while (token.Value != "," && token.Value != ")")
                    {
                        if (ParseSpecifier(token.Value))
                        {
                            prefix += token.Value + " ";
                        }
                        else
                        {
                            type = token.Value;
                        }
                        token = tokens[++i];
                    }

                    XmlElement param = document.CreateElement("Param");
                    param.SetAttribute("Prefix", prefix.Trim());
                    param.SetAttribute("Type", type);
                    param.SetAttribute("Name", $"value{tokenElement.ChildNodes.Count}");

                    tokenElement.AppendChild(param);
                }
            }

            tokenElement.SetAttribute("Name", functionName);

            root.AppendChild(tokenElement);

            return functionName;
        }

        private static bool ParseSpecifier(string token)
        {
            switch (token)
            {
                case "signed":
                case "unsigned":
                case "short":
                case "long":
                case "const":
                case "volatile":
                case "restrict":
                case "_Atomic":
                case "auto":
                case "register":
                case "static":
                case "extern":
                case "_Thread_local":
                case "thread_local":
                case "mutable":
                    return true;
                default:
                    return false;
            }
        }
    }
}
