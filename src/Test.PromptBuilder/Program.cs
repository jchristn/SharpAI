namespace Test.PromptBuilder
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using GetSomeInput;
    using SharpAI.Prompts;

    public static class Program
    {
        static bool _RunForever = true;
        static bool _Debug = false;
        static ChatFormatEnum _CurrentChatFormat = ChatFormatEnum.ChatML;
        static TextGenerationFormatEnum _CurrentTextFormat = TextGenerationFormatEnum.Instruction;

        public static void Main(string[] args)
        {
            while (_RunForever)
            {
                string userInput = Inputty.GetString("Command [? for help]:", null, false);

                if (userInput.Equals("?")) Menu();
                else if (userInput.Equals("q")) _RunForever = false;
                else if (userInput.Equals("cls")) Console.Clear();
                else if (userInput.Equals("debug")) ToggleDebug();
                else if (userInput.Equals("chatfmt")) SetChatFormat();
                else if (userInput.Equals("textfmt")) SetTextFormat();
                else if (userInput.Equals("test1-1")) Test1_1();
                else if (userInput.Equals("test1-2")) Test1_2();
                else if (userInput.Equals("test1-3")) Test1_3();
                else if (userInput.Equals("test1-4")) Test1_4();
                else if (userInput.Equals("test2-1")) Test2_1();
                else if (userInput.Equals("test2-2")) Test2_2();
                else if (userInput.Equals("test2-3")) Test2_3();
                else if (userInput.Equals("test2-4")) Test2_4();
                else if (userInput.Equals("test3-1")) Test3_1();
                else if (userInput.Equals("test3-2")) Test3_2();
                else if (userInput.Equals("demo1")) Demo1();
                else if (userInput.Equals("demo2")) Demo2();
                else
                {
                    string[] parts = userInput.Split(new char[] { ' ' });

                    if (parts.Length == 2)
                    {
                        if (parts[0].Equals("chat"))
                        {
                            if (parts[1].Equals("build")) BuildChat();
                            else if (parts[1].Equals("interactive")) InteractiveChat();
                            else if (parts[1].Equals("formats")) ShowChatFormats();
                        }
                        else if (parts[0].Equals("text"))
                        {
                            if (parts[1].Equals("build")) BuildText();
                            else if (parts[1].Equals("examples")) BuildTextWithExamples();
                            else if (parts[1].Equals("formats")) ShowTextFormats();
                        }
                    }
                }
            }
        }

        static void Menu()
        {
            Console.WriteLine("");
            Console.WriteLine("Available commands:");
            Console.WriteLine("  ?                 help, this menu");
            Console.WriteLine("  q                 quit");
            Console.WriteLine("  cls               clear the screen");
            Console.WriteLine("  debug             enable or disable debug (enabled: " + _Debug + ")");
            Console.WriteLine("");
            Console.WriteLine("  chatfmt           set the chat format (currently " + _CurrentChatFormat + ")");
            Console.WriteLine("  textfmt           set the text generation format (currently " + _CurrentTextFormat + ")");
            Console.WriteLine("");
            Console.WriteLine("  test1-1           test simple chat format");
            Console.WriteLine("  test1-2           test ChatML format");
            Console.WriteLine("  test1-3           test Llama formats");
            Console.WriteLine("  test1-4           test other chat formats");
            Console.WriteLine("  test2-1           test text generation - basic formats");
            Console.WriteLine("  test2-2           test text generation - creative formats");
            Console.WriteLine("  test2-3           test text generation - with examples");
            Console.WriteLine("  test2-4           test text generation - helper methods");
            Console.WriteLine("  test3-1           test chat prompt with different roles");
            Console.WriteLine("  test3-2           test text prompt with context");
            Console.WriteLine("");
            Console.WriteLine("  demo1             comprehensive chat format demo");
            Console.WriteLine("  demo2             comprehensive text generation demo");
            Console.WriteLine("");
            Console.WriteLine("  chat build        build a custom chat prompt");
            Console.WriteLine("  chat interactive  interactive chat session");
            Console.WriteLine("  chat formats      show all chat format examples");
            Console.WriteLine("");
            Console.WriteLine("  text build        build a custom text prompt");
            Console.WriteLine("  text examples     build text prompt with examples");
            Console.WriteLine("  text formats      show all text format examples");
            Console.WriteLine("");
        }

        static void ToggleDebug()
        {
            _Debug = !_Debug;
            Console.WriteLine("Debug mode: " + _Debug);
        }

        static void SetChatFormat()
        {
            Console.WriteLine("");
            Console.WriteLine("Available chat formats:");
            foreach (ChatFormatEnum format in Enum.GetValues(typeof(ChatFormatEnum)))
            {
                Console.WriteLine("  " + format);
            }
            Console.WriteLine("");

            string input = Inputty.GetString("Chat format [" + _CurrentChatFormat + "]:", _CurrentChatFormat.ToString(), false);
            if (Enum.TryParse<ChatFormatEnum>(input, out ChatFormatEnum newFormat))
            {
                _CurrentChatFormat = newFormat;
                Console.WriteLine("Chat format set to: " + _CurrentChatFormat);
            }
            else
            {
                Console.WriteLine("Invalid format, keeping: " + _CurrentChatFormat);
            }
        }

        static void SetTextFormat()
        {
            Console.WriteLine("");
            Console.WriteLine("Available text generation formats:");
            foreach (TextGenerationFormatEnum format in Enum.GetValues(typeof(TextGenerationFormatEnum)))
            {
                Console.WriteLine("  " + format);
            }
            Console.WriteLine("");

            string input = Inputty.GetString("Text format [" + _CurrentTextFormat + "]:", _CurrentTextFormat.ToString(), false);
            if (Enum.TryParse<TextGenerationFormatEnum>(input, out TextGenerationFormatEnum newFormat))
            {
                _CurrentTextFormat = newFormat;
                Console.WriteLine("Text format set to: " + _CurrentTextFormat);
            }
            else
            {
                Console.WriteLine("Invalid format, keeping: " + _CurrentTextFormat);
            }
        }

        #region Chat-Tests

        static void Test1_1()
        {
            Console.WriteLine("");
            Console.WriteLine("=== Testing Simple Chat Format ===");

            List<ChatMessage> messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = "You are a helpful assistant." },
                new ChatMessage { Role = "user", Content = "What is 2+2?" },
                new ChatMessage { Role = "assistant", Content = "2+2 equals 4." },
                new ChatMessage { Role = "user", Content = "Why?" }
            };

            string prompt = ChatPromptBuilder.Build(ChatFormatEnum.Simple, messages);
            Console.WriteLine(prompt);
            Console.WriteLine("=== End of Simple Format ===");
            Console.WriteLine("");
        }

        static void Test1_2()
        {
            Console.WriteLine("");
            Console.WriteLine("=== Testing ChatML Format ===");

            List<ChatMessage> messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = "You are a helpful coding assistant." },
                new ChatMessage { Role = "user", Content = "Write a hello world in Python" },
                new ChatMessage { Role = "assistant", Content = "print('Hello, World!')" },
                new ChatMessage { Role = "user", Content = "Now in JavaScript" }
            };

            string prompt = ChatPromptBuilder.Build(ChatFormatEnum.ChatML, messages);
            Console.WriteLine(prompt);
            Console.WriteLine("=== End of ChatML Format ===");
            Console.WriteLine("");
        }

        static void Test1_3()
        {
            Console.WriteLine("");
            Console.WriteLine("=== Testing Llama 2 Format ===");

            List<ChatMessage> messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = "You are an expert in mathematics." },
                new ChatMessage { Role = "user", Content = "Explain calculus" },
                new ChatMessage { Role = "assistant", Content = "Calculus is the mathematical study of continuous change." },
                new ChatMessage { Role = "user", Content = "Give an example" }
            };

            string prompt = ChatPromptBuilder.Build(ChatFormatEnum.Llama2, messages);
            Console.WriteLine(prompt);
            Console.WriteLine("=== End of Llama 2 Format ===");
            Console.WriteLine("");

            Console.WriteLine("=== Testing Llama 3 Format ===");
            prompt = ChatPromptBuilder.Build(ChatFormatEnum.Llama3, messages);
            Console.WriteLine(prompt);
            Console.WriteLine("=== End of Llama 3 Format ===");
            Console.WriteLine("");
        }

        static void Test1_4()
        {
            Console.WriteLine("");
            List<ChatMessage> messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = "You are a creative writer." },
                new ChatMessage { Role = "user", Content = "Write a haiku" }
            };

            Console.WriteLine("=== Testing Alpaca Format ===");
            Console.WriteLine(ChatPromptBuilder.Build(ChatFormatEnum.Alpaca, messages));
            Console.WriteLine("");

            Console.WriteLine("=== Testing Mistral Format ===");
            Console.WriteLine(ChatPromptBuilder.Build(ChatFormatEnum.Mistral, messages));
            Console.WriteLine("");

            Console.WriteLine("=== Testing HumanAssistant Format ===");
            Console.WriteLine(ChatPromptBuilder.Build(ChatFormatEnum.HumanAssistant, messages));
            Console.WriteLine("");

            Console.WriteLine("=== Testing Zephyr Format ===");
            Console.WriteLine(ChatPromptBuilder.Build(ChatFormatEnum.Zephyr, messages));
            Console.WriteLine("");

            Console.WriteLine("=== Testing Phi Format ===");
            Console.WriteLine(ChatPromptBuilder.Build(ChatFormatEnum.Phi, messages));
            Console.WriteLine("");

            Console.WriteLine("=== Testing DeepSeek Format ===");
            Console.WriteLine(ChatPromptBuilder.Build(ChatFormatEnum.DeepSeek, messages));
            Console.WriteLine("");
        }

        #endregion

        #region Text-Tests

        static void Test2_1()
        {
            Console.WriteLine("");
            Console.WriteLine("=== Testing Basic Text Generation Formats ===");

            string input = "Write a story about a robot learning to paint";

            Console.WriteLine("--- Raw Format ---");
            Console.WriteLine(TextGenerationPromptBuilder.Build(TextGenerationFormatEnum.Raw, input));
            Console.WriteLine("");

            Console.WriteLine("--- Completion Format ---");
            Console.WriteLine(TextGenerationPromptBuilder.Build(TextGenerationFormatEnum.Completion, "Once upon a time, in a small village by the sea,"));
            Console.WriteLine("");

            Console.WriteLine("--- Instruction Format ---");
            Console.WriteLine(TextGenerationPromptBuilder.Build(TextGenerationFormatEnum.Instruction, input));
            Console.WriteLine("");

            Console.WriteLine("--- Question Answer Format ---");
            Console.WriteLine(TextGenerationPromptBuilder.Build(TextGenerationFormatEnum.QuestionAnswer, "What are the main causes of climate change?"));
            Console.WriteLine("");
        }

        static void Test2_2()
        {
            Console.WriteLine("");
            Console.WriteLine("=== Testing Creative Text Generation Formats ===");

            Dictionary<string, string> context = new Dictionary<string, string>();

            Console.WriteLine("--- Creative Writing Format ---");
            context["genre"] = "Science Fiction";
            context["style"] = "Descriptive, atmospheric";
            context["length"] = "500 words";
            Console.WriteLine(TextGenerationPromptBuilder.Build(TextGenerationFormatEnum.CreativeWriting, "First contact with aliens", context));
            Console.WriteLine("");

            Console.WriteLine("--- Code Generation Format ---");
            context.Clear();
            context["language"] = "Python";
            context["requirements"] = "Handle edge cases";
            context["constraints"] = "O(log n) time complexity";
            Console.WriteLine(TextGenerationPromptBuilder.Build(TextGenerationFormatEnum.CodeGeneration, "Implement a binary search algorithm", context));
            Console.WriteLine("");

            Console.WriteLine("--- Academic Format ---");
            context.Clear();
            context["type"] = "Research Summary";
            context["field"] = "Computer Science";
            context["audience"] = "Graduate students";
            Console.WriteLine(TextGenerationPromptBuilder.Build(TextGenerationFormatEnum.Academic, "The Impact of AI on Healthcare", context));
            Console.WriteLine("");
        }

        static void Test2_3()
        {
            Console.WriteLine("");
            Console.WriteLine("=== Testing Text Generation with Examples ===");

            List<PromptExample> examples = new List<PromptExample>
            {
                new PromptExample("cat", "cats"),
                new PromptExample("dog", "dogs"),
                new PromptExample("mouse", "mice")
            };

            string prompt = TextGenerationPromptBuilder.BuildWithExamples(
                TextGenerationFormatEnum.Instruction,
                "goose",
                examples
            );

            Console.WriteLine(prompt);
            Console.WriteLine("");
        }

        static void Test2_4()
        {
            Console.WriteLine("");
            Console.WriteLine("=== Testing Helper Methods ===");

            Console.WriteLine("--- Create Continuation ---");
            Console.WriteLine(TextGenerationPromptBuilder.CreateContinuation("The future of technology is"));
            Console.WriteLine("");

            Console.WriteLine("--- Create Instruction (no context) ---");
            Console.WriteLine(TextGenerationPromptBuilder.CreateInstruction("Explain quantum computing in simple terms"));
            Console.WriteLine("");

            Console.WriteLine("--- Create Instruction (with context) ---");
            Console.WriteLine(TextGenerationPromptBuilder.CreateInstruction(
                "Summarize the main points",
                "You are an expert summarizer. Focus on key insights and actionable items."
            ));
            Console.WriteLine("");
        }

        #endregion

        #region Advanced-Tests

        static void Test3_1()
        {
            Console.WriteLine("");
            Console.WriteLine("=== Testing Chat with Multiple Roles ===");

            List<ChatMessage> messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = "You are moderating a debate between two AI assistants." },
                new ChatMessage { Role = "user", Content = "Let's discuss: Is AI consciousness possible?" },
                new ChatMessage { Role = "assistant", Content = "AI-1: I believe consciousness requires subjective experience..." },
                new ChatMessage { Role = "user", Content = "AI-2, your response?" },
                new ChatMessage { Role = "assistant", Content = "AI-2: The question assumes we can define consciousness..." },
                new ChatMessage { Role = "user", Content = "Moderator: Please summarize both positions." }
            };

            foreach (ChatFormatEnum format in new[] { ChatFormatEnum.ChatML, ChatFormatEnum.Llama3, ChatFormatEnum.HumanAssistant })
            {
                Console.WriteLine($"--- Format: {format} ---");
                Console.WriteLine(ChatPromptBuilder.Build(format, messages));
                Console.WriteLine("");
            }
        }

        static void Test3_2()
        {
            Console.WriteLine("");
            Console.WriteLine("=== Testing Complex Text Generation ===");

            Dictionary<string, string> context = new Dictionary<string, string>
            {
                ["characters"] = "Alice (scientist), Bob (journalist)",
                ["setting"] = "Research laboratory",
                ["tone"] = "Professional but tense"
            };

            Console.WriteLine("--- Dialogue Format ---");
            Console.WriteLine(TextGenerationPromptBuilder.Build(TextGenerationFormatEnum.Dialogue, "New discovery about quantum entanglement", context));
            Console.WriteLine("");

            Console.WriteLine("--- List Generation Format ---");
            context.Clear();
            context["count"] = "5";
            context["format"] = "bullet";
            Console.WriteLine(TextGenerationPromptBuilder.Build(TextGenerationFormatEnum.ListGeneration, "innovative uses for blockchain technology", context));
            Console.WriteLine("");

            Console.WriteLine("--- Template Filling Format ---");
            string template = @"Subject: [PROJECT_UPDATE]
Dear [TEAM],

I wanted to update you on the progress of [PROJECT_NAME].

Key achievements:
- [ACHIEVEMENT_1]
- [ACHIEVEMENT_2]

Next steps:
[NEXT_STEPS]

Best regards,
[YOUR_NAME]";
            Console.WriteLine(TextGenerationPromptBuilder.Build(TextGenerationFormatEnum.TemplateFilling, template));
            Console.WriteLine("");
        }

        #endregion

        #region Interactive-Features

        static void BuildChat()
        {
            Console.WriteLine("");
            Console.WriteLine("Building custom chat prompt...");

            List<ChatMessage> messages = new List<ChatMessage>();

            string systemPrompt = Inputty.GetString("System prompt (optional):", null, true);
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new ChatMessage { Role = "system", Content = systemPrompt });
            }

            while (true)
            {
                string role = Inputty.GetString("Role (user/assistant/done):", "user", false);
                if (role.Equals("done", StringComparison.OrdinalIgnoreCase)) break;

                if (!role.Equals("user", StringComparison.OrdinalIgnoreCase) &&
                    !role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Invalid role. Use 'user' or 'assistant'.");
                    continue;
                }

                string content = Inputty.GetString($"{role} message:", null, false);
                messages.Add(new ChatMessage { Role = role, Content = content });
            }

            if (messages.Count > 0)
            {
                Console.WriteLine("");
                Console.WriteLine($"=== Generated {_CurrentChatFormat} Prompt ===");
                Console.WriteLine(ChatPromptBuilder.Build(_CurrentChatFormat, messages));
                Console.WriteLine("=== End of Prompt ===");
            }
        }

        static void InteractiveChat()
        {
            Console.WriteLine("");
            Console.WriteLine("Starting interactive chat session...");
            Console.WriteLine("Type 'exit' to end the session.");
            Console.WriteLine("");

            List<ChatMessage> messages = new List<ChatMessage>();

            string systemPrompt = Inputty.GetString("System prompt (optional):", "You are a helpful assistant.", true);
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new ChatMessage { Role = "system", Content = systemPrompt });
            }

            while (true)
            {
                string userInput = Inputty.GetString("You:", null, false);
                if (userInput.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

                messages.Add(new ChatMessage { Role = "user", Content = userInput });

                if (_Debug)
                {
                    Console.WriteLine("");
                    Console.WriteLine("=== Current Prompt ===");
                    Console.WriteLine(ChatPromptBuilder.Build(_CurrentChatFormat, messages));
                    Console.WriteLine("=== End ===");
                    Console.WriteLine("");
                }

                string assistantResponse = Inputty.GetString("Assistant:", null, false);
                messages.Add(new ChatMessage { Role = "assistant", Content = assistantResponse });
            }

            Console.WriteLine("");
            Console.WriteLine("=== Final Chat Transcript ===");
            Console.WriteLine(ChatPromptBuilder.Build(_CurrentChatFormat, messages));
            Console.WriteLine("=== End of Transcript ===");
        }

        static void ShowChatFormats()
        {
            Console.WriteLine("");
            List<ChatMessage> messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = "You are a helpful assistant." },
                new ChatMessage { Role = "user", Content = "Hello!" },
                new ChatMessage { Role = "assistant", Content = "Hi! How can I help you?" },
                new ChatMessage { Role = "user", Content = "What's the weather?" }
            };

            foreach (ChatFormatEnum format in Enum.GetValues(typeof(ChatFormatEnum)))
            {
                Console.WriteLine($"=== {format} Format ===");
                Console.WriteLine(ChatPromptBuilder.Build(format, messages));
                Console.WriteLine("");
            }
        }

        static void BuildText()
        {
            Console.WriteLine("");
            Console.WriteLine("Building custom text prompt...");

            string input = Inputty.GetString("Input text:", null, false);

            Dictionary<string, string> context = new Dictionary<string, string>();

            Console.WriteLine("Add context parameters? (press Enter to skip)");
            while (true)
            {
                string key = Inputty.GetString("Context key (or empty to finish):", null, true);
                if (string.IsNullOrEmpty(key)) break;

                string value = Inputty.GetString($"Value for '{key}':", null, false);
                context[key] = value;
            }

            Console.WriteLine("");
            Console.WriteLine($"=== Generated {_CurrentTextFormat} Prompt ===");
            Console.WriteLine(TextGenerationPromptBuilder.Build(_CurrentTextFormat, input, context.Count > 0 ? context : null));
            Console.WriteLine("=== End of Prompt ===");
        }

        static void BuildTextWithExamples()
        {
            Console.WriteLine("");
            Console.WriteLine("Building text prompt with examples...");

            List<PromptExample> examples = new List<PromptExample>();

            Console.WriteLine("Add examples (press Enter on input to skip):");
            while (true)
            {
                string exampleInput = Inputty.GetString("Example input:", null, true);
                if (string.IsNullOrEmpty(exampleInput)) break;

                string exampleOutput = Inputty.GetString("Example output:", null, false);
                examples.Add(new PromptExample(exampleInput, exampleOutput));
            }

            string input = Inputty.GetString("Actual input:", null, false);

            Console.WriteLine("");
            Console.WriteLine($"=== Generated {_CurrentTextFormat} Prompt with Examples ===");
            Console.WriteLine(TextGenerationPromptBuilder.BuildWithExamples(_CurrentTextFormat, input, examples));
            Console.WriteLine("=== End of Prompt ===");
        }

        static void ShowTextFormats()
        {
            Console.WriteLine("");
            string input = "Create a comprehensive guide";

            Dictionary<string, string> context = new Dictionary<string, string>
            {
                ["language"] = "Python",
                ["genre"] = "Technical",
                ["count"] = "5",
                ["type"] = "Tutorial"
            };

            foreach (TextGenerationFormatEnum format in Enum.GetValues(typeof(TextGenerationFormatEnum)))
            {
                Console.WriteLine($"=== {format} Format ===");
                Console.WriteLine(TextGenerationPromptBuilder.Build(format, input, context));
                Console.WriteLine("");
            }
        }

        #endregion

        #region Demos

        static void Demo1()
        {
            Console.WriteLine("");
            Console.WriteLine("=== Comprehensive Chat Format Demo ===");
            Console.WriteLine("This demo shows a multi-turn conversation formatted for different models");
            Console.WriteLine("");

            List<ChatMessage> messages = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = "system",
                    Content = "You are a knowledgeable AI assistant specializing in explaining complex topics simply.",
                    Timestamp = DateTime.UtcNow.AddMinutes(-10)
                },
                new ChatMessage
                {
                    Role = "user",
                    Content = "Can you explain how neural networks work?",
                    Timestamp = DateTime.UtcNow.AddMinutes(-8)
                },
                new ChatMessage
                {
                    Role = "assistant",
                    Content = "Neural networks are computing systems inspired by biological brains. They consist of layers of interconnected nodes (neurons) that process information. Each connection has a weight that adjusts during training, allowing the network to learn patterns from data.",
                    Timestamp = DateTime.UtcNow.AddMinutes(-7)
                },
                new ChatMessage
                {
                    Role = "user",
                    Content = "How do they actually learn?",
                    Timestamp = DateTime.UtcNow.AddMinutes(-5)
                },
                new ChatMessage
                {
                    Role = "assistant",
                    Content = "Neural networks learn through a process called backpropagation. When the network makes a prediction, it compares the output to the correct answer and calculates the error. This error is then propagated backwards through the network, adjusting the weights to minimize future errors. This process repeats many times with lots of examples until the network learns to make accurate predictions.",
                    Timestamp = DateTime.UtcNow.AddMinutes(-3)
                },
                new ChatMessage
                {
                    Role = "user",
                    Content = "Can you give me a simple real-world example?",
                    Timestamp = DateTime.UtcNow.AddMinutes(-1)
                }
            };

            // Show timestamps if debug is on
            if (_Debug)
            {
                Console.WriteLine("Message Timeline:");
                foreach (ChatMessage msg in messages)
                {
                    Console.WriteLine($"  [{msg.Timestamp:HH:mm:ss}] {msg.Role}: {msg.Content.Substring(0, Math.Min(50, msg.Content.Length))}...");
                }
                Console.WriteLine("");
            }

            // Generate prompts for each format
            foreach (ChatFormatEnum format in Enum.GetValues(typeof(ChatFormatEnum)))
            {
                Console.WriteLine($"════════════════════════════════════════");
                Console.WriteLine($"Format: {format}");
                Console.WriteLine($"════════════════════════════════════════");

                try
                {
                    string prompt = ChatPromptBuilder.Build(format, messages);
                    Console.WriteLine(prompt);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }

                Console.WriteLine("");
                Console.WriteLine("Press any key to see next format...");
                Console.ReadKey();
                Console.WriteLine("");
            }
        }

        static void Demo2()
        {
            Console.WriteLine("");
            Console.WriteLine("=== Comprehensive Text Generation Demo ===");
            Console.WriteLine("This demo shows various text generation formats with rich contexts");
            Console.WriteLine("");

            // Demo 1: Code Generation with Examples
            Console.WriteLine("1. Code Generation with Few-Shot Examples");
            Console.WriteLine("─────────────────────────────────────────");

            List<PromptExample> codeExamples = new List<PromptExample>
            {
                new PromptExample("reverse a string", @"def reverse_string(s):
    return s[::-1]"),
                new PromptExample("check if palindrome", @"def is_palindrome(s):
    s = s.lower().replace(' ', '')
    return s == s[::-1]"),
                new PromptExample("find maximum in list", @"def find_max(lst):
    return max(lst) if lst else None")
            };

            Dictionary<string, string> codeContext = new Dictionary<string, string>
            {
                ["language"] = "Python",
                ["requirements"] = "Include error handling and type hints",
                ["constraints"] = "Use built-in functions where possible"
            };

            string codePrompt = TextGenerationPromptBuilder.BuildWithExamples(
                TextGenerationFormatEnum.CodeGeneration,
                "merge two sorted lists",
                codeExamples,
                codeContext
            );

            Console.WriteLine(codePrompt);
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();

            // Demo 2: Creative Writing
            Console.WriteLine("\n2. Creative Writing with Rich Context");
            Console.WriteLine("─────────────────────────────────────");

            Dictionary<string, string> storyContext = new Dictionary<string, string>
            {
                ["genre"] = "Cyberpunk Mystery",
                ["style"] = "First-person narrative, noir-influenced",
                ["length"] = "Flash fiction (500-1000 words)",
                ["setting"] = "Neo-Tokyo, 2087",
                ["protagonist"] = "A cyborg detective",
                ["theme"] = "What defines humanity in a digital age?"
            };

            Console.WriteLine(TextGenerationPromptBuilder.Build(
                TextGenerationFormatEnum.CreativeWriting,
                "A detective investigates murders in virtual reality",
                storyContext
            ));

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();

            // Demo 3: Academic Writing
            Console.WriteLine("\n3. Academic Abstract Generation");
            Console.WriteLine("───────────────────────────────");

            Dictionary<string, string> academicContext = new Dictionary<string, string>
            {
                ["type"] = "Research Paper Abstract",
                ["field"] = "Artificial Intelligence",
                ["audience"] = "ML researchers and practitioners",
                ["section"] = "Abstract",
                ["keywords"] = "transformer models, attention mechanisms, NLP"
            };

            Console.WriteLine(TextGenerationPromptBuilder.Build(
                TextGenerationFormatEnum.Academic,
                "Improving Transformer Efficiency Through Selective Attention",
                academicContext
            ));

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();

            // Demo 4: Complex Dialogue
            Console.WriteLine("\n4. Multi-Character Dialogue");
            Console.WriteLine("──────────────────────────");

            Dictionary<string, string> dialogueContext = new Dictionary<string, string>
            {
                ["characters"] = "Dr. Sarah Chen (AI researcher), Marcus Webb (tech journalist), ARIA (AI system)",
                ["setting"] = "Live interview at TechCon 2025",
                ["tone"] = "Professional but with underlying tension",
                ["conflict"] = "Ethics of conscious AI",
                ["firstCharacter"] = "Marcus Webb"
            };

            Console.WriteLine(TextGenerationPromptBuilder.Build(
                TextGenerationFormatEnum.Dialogue,
                "The announcement of the first allegedly conscious AI",
                dialogueContext
            ));

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();

            // Demo 5: List Generation with Structure
            Console.WriteLine("\n5. Structured List Generation");
            Console.WriteLine("─────────────────────────────");

            Dictionary<string, string> listContext = new Dictionary<string, string>
            {
                ["count"] = "7",
                ["format"] = "numbered",
                ["detail_level"] = "Include brief explanation for each",
                ["target_audience"] = "Startup founders"
            };

            Console.WriteLine(TextGenerationPromptBuilder.Build(
                TextGenerationFormatEnum.ListGeneration,
                "innovative AI business models for 2025",
                listContext
            ));

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();

            // Demo 6: Template with Complex Structure
            Console.WriteLine("\n6. Complex Template Filling");
            Console.WriteLine("───────────────────────────");

            string complexTemplate = @"PROJECT PROPOSAL: [PROJECT_NAME]

Executive Summary:
[EXECUTIVE_SUMMARY]

Problem Statement:
The current challenge facing [INDUSTRY] is [PROBLEM_DESCRIPTION]. This results in:
- [CONSEQUENCE_1]
- [CONSEQUENCE_2]
- [CONSEQUENCE_3]

Proposed Solution:
We propose developing [SOLUTION_NAME], which will:
1. [BENEFIT_1]
2. [BENEFIT_2]
3. [BENEFIT_3]

Technical Approach:
[TECHNICAL_DETAILS]

Timeline:
- Phase 1 ([PHASE_1_DURATION]): [PHASE_1_GOALS]
- Phase 2 ([PHASE_2_DURATION]): [PHASE_2_GOALS]
- Phase 3 ([PHASE_3_DURATION]): [PHASE_3_GOALS]

Budget Estimate: [BUDGET_RANGE]

Success Metrics:
[SUCCESS_METRICS]

Team:
[TEAM_COMPOSITION]";

            Console.WriteLine(TextGenerationPromptBuilder.Build(
                TextGenerationFormatEnum.TemplateFilling,
                complexTemplate
            ));

            Console.WriteLine("\n=== End of Demo ===");
        }

        #endregion
    }
}