using System.Collections.Generic;
using SharpAI.Helpers;
using SharpAI.Prompts;
using Xunit;

namespace SharpAI.Tests;

public class PromptSupportTests
{
    [Theory]
    [InlineData("qwen3.5", ChatFormatEnum.ChatML)]
    [InlineData("qwen-3.5", ChatFormatEnum.ChatML)]
    [InlineData("qwen35", ChatFormatEnum.ChatML)]
    [InlineData("gemma4", ChatFormatEnum.Gemma)]
    [InlineData("gemma-4", ChatFormatEnum.Gemma)]
    [InlineData("google-gemma-4", ChatFormatEnum.Gemma)]
    public void ModelFamilyToChatFormatMapsNewFamilies(string family, ChatFormatEnum expected)
    {
        Assert.Equal(expected, ChatFormatHelper.ModelFamilyToChatFormat(family));
    }

    [Fact]
    public void GemmaPromptBuilderKeepsGemmaTurnMarkers()
    {
        List<ChatMessage> messages =
        [
            new ChatMessage { Role = "system", Content = "You are concise." },
            new ChatMessage { Role = "user", Content = "Summarize this." }
        ];

        string prompt = ChatPromptBuilder.Build(ChatFormatHelper.ModelFamilyToChatFormat("gemma4"), messages);

        Assert.Contains("<start_of_turn>user", prompt);
        Assert.Contains("<end_of_turn>", prompt);
        Assert.EndsWith("<start_of_turn>model" + System.Environment.NewLine, prompt);
    }

    [Fact]
    public void ThinkingFilterRemovesThinkingBlocksForQwenStyleOutput()
    {
        string response = "<think>private reasoning</think>\nVisible answer";

        string filtered = ThinkingFilter.RemoveThinkingBlocks(response);

        Assert.Equal("Visible answer", filtered);
    }

    [Fact]
    public void ChatMLDefaultsAreReturnedForQwen35()
    {
        ChatFormatEnum format = ChatFormatHelper.ModelFamilyToChatFormat("qwen3.5");
        string[] stops = ChatFormatHelper.GetDefaultStopSequences(format);

        Assert.Contains("<|im_end|>", stops);
    }
}
