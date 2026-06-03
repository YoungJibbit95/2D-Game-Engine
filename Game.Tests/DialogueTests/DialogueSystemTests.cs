using Game.Core.Data;
using Game.Core.Dialogue;
using Xunit;

namespace Game.Tests.DialogueTests;

public sealed class DialogueSystemTests
{
    [Fact]
    public void Loader_ReadsDialogueDefinitionJson()
    {
        var dialogue = new DialogueDefinitionJsonLoader().LoadDefinitionFromJson("""
        {
          "id": "intro",
          "displayName": "Intro",
          "startNodeId": "hello",
          "tags": ["starter"],
          "nodes": [
            {
              "id": "hello",
              "speakerId": "guide",
              "text": "Hello.",
              "options": [
                { "text": "Continue", "targetNodeId": "next" }
              ]
            },
            {
              "id": "next",
              "speakerId": "guide",
              "text": "Good luck.",
              "endsConversation": true
            }
          ]
        }
        """);

        Assert.Equal("intro", dialogue.Id);
        Assert.True(dialogue.HasTag("starter"));
        Assert.Equal("hello", dialogue.StartNodeId);
        Assert.Equal(2, dialogue.Nodes.Count);
        Assert.True(dialogue.TryGetNode("hello", out var node));
        Assert.Equal("guide", node.SpeakerId);
        Assert.Single(node.Options);
    }

    [Fact]
    public void Registry_RejectsMissingOptionTarget()
    {
        var dialogue = new DialogueDefinition
        {
            Id = "bad",
            DisplayName = "Bad",
            StartNodeId = "start",
            Nodes = new[]
            {
                new DialogueNodeDefinition
                {
                    Id = "start",
                    Text = "Pick.",
                    Options = new[]
                    {
                        new DialogueOptionDefinition { Text = "Missing", TargetNodeId = "missing" }
                    }
                }
            }
        };

        Assert.Throws<RegistryValidationException>(() => DialogueRegistry.Create(new[] { dialogue }));
    }

    [Fact]
    public void DialogueSystem_StartsAdvancesAndEndsConversation()
    {
        var registry = DialogueRegistry.Create(new[] { CreateDialogue() });
        var system = new DialogueSystem();

        var started = system.Start(registry, "farm_intro");

        Assert.True(started.Success);
        Assert.Equal("hello", started.Node!.Id);
        Assert.False(started.Ended);

        var next = system.Advance(registry, started.Session!, optionIndex: 0);

        Assert.True(next.Success);
        Assert.Equal("advice", next.Node!.Id);
        Assert.Equal("ask_advice", next.SelectedOption!.ActionId);

        var ended = system.Advance(registry, next.Session!);

        Assert.True(ended.Success);
        Assert.True(ended.Ended);
        Assert.Equal(new[] { "hello", "advice" }, ended.Session!.VisitedNodeIds);
    }

    [Fact]
    public void DialogueSystem_StopsAtNodeWithOptionsUntilOptionIsSelected()
    {
        var registry = DialogueRegistry.Create(new[] { CreateDialogue() });
        var system = new DialogueSystem();
        var session = system.Start(registry, "farm_intro").Session!;

        var result = system.Advance(registry, session);

        Assert.True(result.Success);
        Assert.False(result.Ended);
        Assert.Equal("hello", result.Node!.Id);
    }

    private static DialogueDefinition CreateDialogue()
    {
        return new DialogueDefinition
        {
            Id = "farm_intro",
            DisplayName = "Farm Intro",
            StartNodeId = "hello",
            Nodes = new[]
            {
                new DialogueNodeDefinition
                {
                    Id = "hello",
                    Text = "Welcome.",
                    Options = new[]
                    {
                        new DialogueOptionDefinition
                        {
                            Text = "Any advice?",
                            TargetNodeId = "advice",
                            ActionId = "ask_advice"
                        }
                    }
                },
                new DialogueNodeDefinition
                {
                    Id = "advice",
                    Text = "Water your crops.",
                    EndsConversation = true
                }
            }
        };
    }
}
