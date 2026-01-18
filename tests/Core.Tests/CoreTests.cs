using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Linq;
using FrozenFrogFramework.NolanTech;

namespace FrozenFrogFramework.Tests;

[TestFixture]
public class CoreTests
{
    public static StatMock Stat => new StatMock();
    public static RulebookMock Rulebook => new RulebookMock();

    [Description("Nolan scripts use cases for test purpose.")]
    static IEnumerable<TestCaseData> NolanParsingUseCases()
    {
        TextMock Textbook = new TextMock()

        .Text("HELLO", "Hello World")
        .Text("QUEST", "I Love King Quest")
        .Text("TALK", "Accepterez-vous ma quête preu chevalier?")
        .Text("THANK", "Merci mon valeureux ...", "et bonne chance!")
        .Text("TALK-1S", "Oui.")
        .Text("TALK-1", "Oui votre majesté.")
        .Text("TALK-2S", "Peut-être?")
        .Text("TALK-2", "Ça dépend, c'est quoi?")
        .Text("TALK-2-0", "Libérez le royaume du dragon.", "Tuez la bête.")
        .Text("TALK-2-1S", "Oui.")
        .Text("TALK-2-1", "Oui mais j'ai besoin d'une arme.")
        .Text("TALK-2-1-0", "Prenez l'épée royale mon brave.")
        .Text("TALK-2-2S", "Non!")
        .Text("TALK-2-2", "Non, je suis contre la cruauté animale.")
        .Text("TALK-3S", "Non!")
        .Text("TALK-3", "Non, merci.")
        .Text("WORRY", "Restez prudent avec ça.", "Ça a déjà coupé des têtes!")

        .Route("TALK", "TALK")
        .Flow("TALK-1S", "TALK-1").Payload("sword")
        .Flow("TALK-2S", "TALK-2")
        .Flow("TALK-3S", "TALK-3")
        .Route("TALK-1", "TALK-1").Goto("THANK")
        .Route("TALK-2", "TALK-2-0", "TALK-2")
        .Flow("TALK-2-1S", "TALK-2-1").Payload("sword")
        .Flow("TALK-2-2S", "TALK-2-2")
        .Route("TALK-2-1", "TALK-2-1-0", "TALK-2-1").Goto("THANK")
        .Route("TALK-2-2", "TALK-2-2")
        .Route("TALK-3", "TALK-3");

        yield return new TestCaseData(
            new string[] {
                "== KINGQUEST [CAVE, castle;dragon] [HOME, door;hero;king]",
                "++ ()-[?hero]->() #HELLO",
                "Hello World",
                "++ ()-[?hero.sword]->() #QUEST",
                "I Love King Quest",
                "++ ()-[?door<CAVE>]->()",
                "++ (dragon<CAVE>)-{hero}[?door<CAVE>]->(dragon.fire<CAVE>;hero.fire<CAVE>)",
                "++ ()-[?castle<HOME>]->()",
                "++ (!sword;!hero.sword)-[?king]->() #TALK",
                "Accepterez-vous ma quête preu chevalier?",
                "-- Oui[.] votre majesté.",
                "--> (sword) THANK",
                "-- [Peut-être?] Ça dépend, c'est quoi?",
                "--- (?<Libérez le royaume du dragon.><Tuez la bête.>)",
                "---- Oui[.] mais j'ai besoin d'une arme.",
                "----- Prenez l'épée royale mon brave.",
                "----> (sword) THANK",
                "---- Non[!], je suis contre la cruauté animale.",
                "-- Non[!], merci.",
                "++ (?sword)-[?king]->() #THANK",
                "Merci mon valeureux ...</>et bonne chance!",
                "++ (?hero.sword)-[?king]->() #WORRY",
                "Restez prudent avec ça.|Ça a déjà coupé des têtes!"
            },
            "KINGQUEST",
            Stat.Mock("CAVE", "castle", "dragon")
                .Mock("HOME", "door", "hero", "king")
                .Mock("LOOP", "TALK-2-0_0").Data,
            Rulebook.Mock("?hero", "HELLO")
                    .Mock("?hero.sword", "QUEST")
                    .Mock("?door<CAVE>")
                    .Mock("?door<CAVE>").Context("dragon<CAVE>").Cost(false, "hero").Payload("dragon.fire<CAVE>", "hero.fire<CAVE>")
                    .Mock("?castle<HOME>")
                    .Mock("?king", "TALK").Context("!hero.sword", "!sword")
                    .Mock("?king", "THANK").Context("?sword")
                    .Mock("?king", "WORRY").Context("?hero.sword").Data,
            Textbook
        ).SetName("Full Script");

        yield return new TestCaseData(
            new string[] {
                "== BOF [HOME, hero;king]",
                "++ (?hero)-[?king]->() #TALK",
                "(!<Accepterez-vous ma quête preu chevalier?><Sauverez-vous le royaume mon ami?>)"
            },
            "BOF",
            Stat.Mock("HOME", "hero", "king").Mock("ONCE", "TALK_0").Data,
            Rulebook.Mock("?king", "TALK").Context("?hero").Data,
            new TextMock().Text("TALK", "Accepterez-vous ma quête preu chevalier?", "Sauverez-vous le royaume mon ami?")
        ).SetName("Simple Talk");
    }

    [Test, TestCaseSource(nameof(NolanParsingUseCases))]
    public void NolanParsingTest(string[] script, string expectedProductName, in F3NolanStatData expectedInitialStat, in F3NolanRuleData[] expectedRulebook, in TextMock expectedTextbook)
    {
        F3NolanScriptBuilder builder = new F3NolanScriptBuilder();

        if (F3NolanScriptBuilder.Parse(script, out List<F3NolanScriptBuilder.Line> lines))
        {
            builder.Build(in lines, out Dictionary<int, object> parts);

            if (builder.Keys.Contains(expectedProductName))
            {
                F3NolanScriptData product = builder[expectedProductName];

                string result = NolanJsonSerializer.SerializeNolanScript(product, prettyPrint: true);

                Assert.That(product.InitialStat, Is.EqualTo(expectedInitialStat), "The stat '" + product.InitialStat.ToString() + "' does not match the\n  expected '" + expectedInitialStat.ToString() + "' value.\n" + result);

                int actualRuleIndex = 0;

                foreach (F3NolanRuleData rule in expectedRulebook)
                {
                    if (actualRuleIndex >= product.RuleBook.Count())
                    {
                        Assert.Fail("The expected rule '" + rule.ToString() + "' was not found.\n" + result);
                    }

                    Assert.That(product.RuleBook[actualRuleIndex].ToString(), Is.EqualTo(rule.ToString()), "The rule '" + product.RuleBook[actualRuleIndex].ToString() + "' does not match the\n  expected '" + rule.ToString() + "' value.\n" + result);

                    string actualTextKey = product.RuleBook[actualRuleIndex].HasText ? product.RuleBook[actualRuleIndex].Text : string.Empty;

                    if (rule.HasText)
                    {
                        Assert.That(actualTextKey, Is.EqualTo(rule.Text), "The rule '" + product.RuleBook[actualRuleIndex].ToString() + "' text '" + actualTextKey + "' does not match the expected '" + rule.Text + "' key.\n" + result);
                    }
                    else if (string.IsNullOrEmpty(actualTextKey) == false)
                    {
                        Assert.Fail("The rule '" + product.RuleBook[actualRuleIndex].ToString() + "' has unexpected text '" + actualTextKey + "' key.\n" + result);
                    }

                    ++actualRuleIndex;
                }

                if (actualRuleIndex < product.RuleBook.Count())
                {
                    Assert.Fail("The actual rule '" + product.RuleBook[actualRuleIndex].ToString() + "' was not expected.\n" + result);
                }

                Dictionary<string, List<string>> expectedText = expectedTextbook.TextData;

                foreach (var pair in product.TextBook.GetLineIterator())
                {
                    Assert.That(expectedText.ContainsKey(pair.Key), Is.True, $"The text key '{pair.Key}' is not an expected value.\n" + result);

                    for (int i = 0; i < pair.Value.Count(); i++) // Assert each line of the textbook
                    {
                        Assert.That(pair.Value[i], Is.EqualTo(expectedText[pair.Key][i]), $"The text '{pair.Key}' line {i + 1} ('{pair.Value[i]}') does\n  not match the expected ('{expectedText[pair.Key][i]}') value.\n" + result);
                    }

                    Assert.That(pair.Value.Count, Is.EqualTo(expectedText[pair.Key].Count), $"The text '{pair.Key}' count ({pair.Value.Count()}) does not match the expected ({expectedText[pair.Key].Count}) value.\n" + result);
                }

                Assert.That(product.TextBook.GetLineIterator().Count(), Is.EqualTo(expectedText.Count), "The text count (" + product.TextBook.GetLineIterator().Count() + ") does not match the expected (" + expectedText.Count + ") value.\n" + result);

                Dictionary<string, F3NolanRouteStruct> expectedRoutes = expectedTextbook.RouteData;

                foreach (var pair in product.TextBook.GetRouteIterator()) // Assert each route of the textbook
                {
                    string routeText = string.Join("', '", pair.Value.Text);
                    Assert.That(expectedRoutes.ContainsKey(pair.Key), Is.True, $"The route key '{pair.Key}' : '{routeText}' is not an expected value.\n");

                    F3NolanRouteStruct actualRoute = pair.Value;
                    F3NolanRouteStruct expectedRoute = expectedRoutes[pair.Key];

                    Assert.That(actualRoute.Text.ToString(), Is.EqualTo(expectedRoute.Text.ToString()), $"The text '{actualRoute.Text.ToString()}' in route '{pair.Key}' does not match the expected '{expectedRoute.Text.ToString()}' value.\n" + result);
                    Assert.That(actualRoute.Flow.Count(), Is.EqualTo(expectedRoute.Flow.Count()), $"The flow count in route '{pair.Key}' does not match.\n" + result);

                    for (int i = 0; i < actualRoute.Flow.Count(); i++)
                    {
                        F3NolanStitchStruct actualStitch = actualRoute.Flow[i];
                        F3NolanStitchStruct expectedStitch = expectedRoute.Flow[i];

                        Assert.That(actualStitch.Choice, Is.EqualTo(expectedStitch.Choice), $"The text key '{actualStitch.Choice}' at flow index {i} in route '{pair.Key}' does not match the expected '{expectedStitch.Choice}' value.\n" + result);
                        Assert.That(actualStitch.Next, Is.EqualTo(expectedStitch.Next), $"The next key '{actualStitch.Next}' at flow index {i} in route '{pair.Key}' does not match the expected '{expectedStitch.Next}' value.\n" + result);

                        Assert.That(actualStitch.Context.ToString(), Is.EqualTo(expectedStitch.Context.ToString()), $"The context '{actualStitch.Context.ToString()}' value at flow index {i} in route '{pair.Key}' do not match the expected '{expectedStitch.Context.ToString()}' value.\n" + result);
                        Assert.That(actualStitch.Cost.ToString(), Is.EqualTo(expectedStitch.Cost.ToString()), $"The cost '{actualStitch.Cost.ToString()}' value at flow index {i} in route '{pair.Key}' do not match the expected '{expectedStitch.Cost.ToString()}' value.\n" + result);
                        Assert.That(actualStitch.Payload.ToString(), Is.EqualTo(expectedStitch.Payload.ToString()), $"The payload '{actualStitch.Payload.ToString()}' value at flow index {i} in route '{pair.Key}' do not match the expected '{expectedStitch.Payload.ToString()}' value.\n" + result);
                        Assert.That(actualStitch.Gain.ToString(), Is.EqualTo(expectedStitch.Gain.ToString()), $"The gain '{actualStitch.Gain.ToString()}' value at flow index {i} in route '{pair.Key}' do not match the expected '{expectedStitch.Gain.ToString()}' value.\n" + result);
                    }

                    Assert.That(actualRoute.Goto ?? string.Empty, Is.EqualTo(expectedRoute.Goto ?? string.Empty), $"The goto '{actualRoute.Goto ?? string.Empty}' in route '{pair.Key}' do not match the expected '{expectedRoute.Goto ?? string.Empty}' value.\n" + result);
                }

                Assert.That(product.TextBook.Routes.Count, Is.EqualTo(expectedRoutes.Count), "The route count (" + product.TextBook.Routes.Count + ") does not match the expected (" + expectedRoutes.Count + ") value.\n");

                Assert.Pass(result);
            }
            else
            {
                Assert.Fail("Failed to parse script for '" + expectedProductName + "' (values: '" + string.Join("', '", builder.Keys) +"').");
            }
        }
        else
        {
            Assert.Fail("Failed to parse script into lines.");
        }
    }
}
