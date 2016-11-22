﻿#region using directives

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using PoGo.NecroBot.Logic.Common;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.PoGoUtils;
using PoGo.NecroBot.Logic.State;
using POGOProtos.Data;
using POGOProtos.Enums;
using POGOProtos.Inventory.Item;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System.Threading.Tasks;

#endregion

namespace PoGo.NecroBot.Logic.Service
{
    public class TelegramService
    {
        private DateTime _lastLoginTime;
        private readonly TelegramBotClient _bot;
        private bool _loggedIn;
        private readonly ISession _session;

        public TelegramService(string apiKey, ISession session)
        {
            try
            {
                _bot = new TelegramBotClient(apiKey);
                _session = session;

                var me = _bot.GetMeAsync().Result;

                _bot.OnMessage += OnTelegramMessageReceived;
                _bot.StartReceiving();

                _session.EventDispatcher.Send(new NoticeEvent { Message = "Using TelegramAPI with " + me.Username });
            }
            catch (Exception)
            {
                _session.EventDispatcher.Send(new ErrorEvent { Message = "Unkown Telegram Error occured. " });
            }
        }

        private async void OnTelegramMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;
            if (message == null || message.Type != MessageType.TextMessage)
                return;

            var answerTextmessage = "";

            if (_session.Profile == null || _session.Inventory == null)
            {
                return;
            }

            var messagetext = message.Text.ToLower().Split(' ');

            if (!_loggedIn && messagetext[0].ToLower().Contains("/login"))
            {
                if (messagetext.Length == 2)
                {
                    if (messagetext[1].ToLower().Contains(_session.LogicSettings.TelegramPassword))
                    {
                        _loggedIn = true;
                        _lastLoginTime = DateTime.Now;
                        answerTextmessage += _session.Translation.GetTranslation(TranslationString.LoggedInTelegram);
                        await SendMessage(message.Chat.Id, answerTextmessage);
                        return;
                    }
                    answerTextmessage += _session.Translation.GetTranslation(TranslationString.LoginFailedTelegram);
                    await SendMessage(message.Chat.Id, answerTextmessage);
                    return;
                }
                answerTextmessage += _session.Translation.GetTranslation(TranslationString.NotLoggedInTelegram);
                await SendMessage(message.Chat.Id, answerTextmessage);
                return;
            }
            if (_loggedIn)
            {
                if (_lastLoginTime.AddMinutes(5).Ticks < DateTime.Now.Ticks)
                {
                    _loggedIn = false;
                    answerTextmessage += _session.Translation.GetTranslation(TranslationString.NotLoggedInTelegram);
                    await SendMessage(message.Chat.Id, answerTextmessage);
                    return;
                }
                var remainingMins = _lastLoginTime.AddMinutes(5).Subtract(DateTime.Now).Minutes;
                var remainingSecs = _lastLoginTime.AddMinutes(5).Subtract(DateTime.Now).Seconds;
                answerTextmessage += _session.Translation.GetTranslation(TranslationString.LoginRemainingTime,
                    remainingMins, remainingSecs);
                await SendMessage(message.Chat.Id, answerTextmessage);
                return;
            }

            switch (messagetext[0].ToLower())
            {
                case "/top":
                    var times = 10;
                    var sortby = "cp";

                    if (messagetext.Length >= 2)
                    {
                        sortby = messagetext[1];
                    }
                    if (messagetext.Length == 3)
                    {
                        try
                        {
                            times = Convert.ToInt32(messagetext[2]);
                        }
                        catch (FormatException)
                        {
                            await SendMessage(message.Chat.Id,
                                _session.Translation.GetTranslation(TranslationString.UsageHelp, "/top [cp/iv] [amount]"));
                            break;
                        }
                    }
                    else if (messagetext.Length > 3)
                    {
                        await SendMessage(message.Chat.Id,
                            _session.Translation.GetTranslation(TranslationString.UsageHelp, "/top [cp/iv] [amount]"));
                        break;
                    }

                    IEnumerable<PokemonData> topPokemons;
                    if (sortby.Equals("iv"))
                    {
                        topPokemons = _session.Inventory.GetHighestsPerfect(times);
                    }
                    else if (sortby.Equals("cp"))
                    {
                        topPokemons = _session.Inventory.GetHighestsCp(times);
                    }
                    else
                    {
                        await SendMessage(message.Chat.Id,
                            _session.Translation.GetTranslation(TranslationString.UsageHelp, "/top [cp/iv] [amount]"));
                        break;
                    }

                    foreach (var pokemon in topPokemons)
                    {
                        answerTextmessage += _session.Translation.GetTranslation(TranslationString.ShowPokeSkillTemplate,
                        pokemon.Cp, PokemonInfo.CalculatePokemonPerfection(pokemon).ToString("0.00"),
                        _session.Translation.GetPokemonMovesetTranslation(PokemonInfo.GetPokemonMove1(pokemon)),
                        _session.Translation.GetPokemonMovesetTranslation(PokemonInfo.GetPokemonMove2(pokemon)),
                        _session.Translation.GetPokemonTranslation(pokemon.PokemonId));

                        if (answerTextmessage.Length > 3800)
                        {
                            await SendMessage(message.Chat.Id, answerTextmessage);
                            answerTextmessage = "";
                        }
                    }
                    await SendMessage(message.Chat.Id, answerTextmessage);
                    break;

                case "/all":
                    var myPokemons = _session.Inventory.GetPokemons();
                    var allMyPokemons = myPokemons.ToList();
                    var allPokemons = _session.Inventory.GetHighestsCp(allMyPokemons.Count);

                    if (messagetext.Length == 1)
                    {
                        allPokemons = _session.Inventory.GetHighestsCp(allMyPokemons.Count);
                    }
                    else if (messagetext.Length == 2)
                    {
                        if (messagetext[1] == "iv")
                        {
                            allPokemons = _session.Inventory.GetHighestsPerfect(allMyPokemons.Count);
                        }
                        else if (messagetext[1] != "cp")
                        {
                            await SendMessage(message.Chat.Id,
                                _session.Translation.GetTranslation(TranslationString.UsageHelp, "/all [cp/iv]"));
                            break;
                        }
                    }
                    else
                    {
                        await SendMessage(message.Chat.Id,
                            _session.Translation.GetTranslation(TranslationString.UsageHelp, "/all [cp/iv]"));
                        break;
                    }

                    foreach (var pokemon in allPokemons)
                    {
                        answerTextmessage += _session.Translation.GetTranslation(TranslationString.ShowPokeTemplate,
                            pokemon.Cp, PokemonInfo.CalculatePokemonPerfection(pokemon).ToString("0.00"),
                            _session.Translation.GetPokemonTranslation(pokemon.PokemonId));

                        if (answerTextmessage.Length > 3800)
                        {
                            await SendMessage(message.Chat.Id, answerTextmessage);
                            answerTextmessage = "";
                        }
                    }
                    await SendMessage(message.Chat.Id, answerTextmessage);
                    break;

                case "/profile":
                    var stats = _session.Inventory.GetPlayerStats();
                    var stat = stats.FirstOrDefault();

                    var myPokemons2 = _session.Inventory.GetPokemons();
                    if (stat != null)
                        answerTextmessage += _session.Translation.GetTranslation(
                            TranslationString.ProfileStatsTemplateString, stat.Level, _session.Profile.PlayerData.Username,
                            stat.Experience, stat.NextLevelXp, stat.PokemonsCaptured, stat.PokemonDeployed,
                            stat.PokeStopVisits, stat.EggsHatched, stat.Evolutions, stat.UniquePokedexEntries, stat.KmWalked,
                            myPokemons2.ToList().Count, _session.Profile.PlayerData.MaxPokemonStorage);
                    await SendMessage(message.Chat.Id, answerTextmessage);
                    break;

                case "/pokedex":
                    var pokedex = _session.Inventory.GetPokeDexItems();
                    var pokedexSort = pokedex.OrderBy(x => x.InventoryItemData.PokedexEntry.PokemonId);

                    answerTextmessage += _session.Translation.GetTranslation(TranslationString.PokedexCatchedTelegram);
                    foreach (var pokedexItem in pokedexSort)
                    {
                        answerTextmessage +=
                            _session.Translation.GetTranslation(TranslationString.PokedexPokemonCatchedTelegram,
                                Convert.ToInt32(pokedexItem.InventoryItemData.PokedexEntry.PokemonId),
                                _session.Translation.GetPokemonTranslation(
                                    pokedexItem.InventoryItemData.PokedexEntry.PokemonId),
                                pokedexItem.InventoryItemData.PokedexEntry.TimesCaptured,
                                pokedexItem.InventoryItemData.PokedexEntry.TimesEncountered);

                        if (answerTextmessage.Length > 3800)
                        {
                            await SendMessage(message.Chat.Id, answerTextmessage);
                            answerTextmessage = "";
                        }
                    }

                    var pokemonsToCapture =
                        Enum.GetValues(typeof(PokemonId))
                            .Cast<PokemonId>()
                            .Except(pokedex.Select(x => x.InventoryItemData.PokedexEntry.PokemonId));

                    await SendMessage(message.Chat.Id, answerTextmessage);
                    answerTextmessage = "";

                    answerTextmessage += _session.Translation.GetTranslation(TranslationString.PokedexNeededTelegram);

                    foreach (var pokedexItem in pokemonsToCapture)
                    {
                        if (Convert.ToInt32(pokedexItem) > 0)
                        {
                            answerTextmessage +=
                                _session.Translation.GetTranslation(TranslationString.PokedexPokemonNeededTelegram,
                                    Convert.ToInt32(pokedexItem), _session.Translation.GetPokemonTranslation(pokedexItem));

                            if (answerTextmessage.Length > 3800)
                            {
                                await SendMessage(message.Chat.Id, answerTextmessage);
                                answerTextmessage = "";
                            }
                        }
                    }
                    await SendMessage(message.Chat.Id, answerTextmessage);

                    break;

                case "/loc":
                    SendLocation(message.Chat.Id, _session.Client.CurrentLatitude, _session.Client.CurrentLongitude);
                    break;

                case "/items":
                    var inventory = _session.Inventory;
                    answerTextmessage += _session.Translation.GetTranslation(TranslationString.CurrentPokeballInv,
                        inventory.GetItemAmountByType(ItemId.ItemPokeBall),
                        inventory.GetItemAmountByType(ItemId.ItemGreatBall),
                        inventory.GetItemAmountByType(ItemId.ItemUltraBall),
                        inventory.GetItemAmountByType(ItemId.ItemMasterBall));
                    answerTextmessage += "\n";
                    answerTextmessage += _session.Translation.GetTranslation(TranslationString.CurrentPotionInv,
                        inventory.GetItemAmountByType(ItemId.ItemPotion),
                        inventory.GetItemAmountByType(ItemId.ItemSuperPotion),
                        inventory.GetItemAmountByType(ItemId.ItemHyperPotion),
                        inventory.GetItemAmountByType(ItemId.ItemMaxPotion));
                    answerTextmessage += "\n";
                    answerTextmessage += _session.Translation.GetTranslation(TranslationString.CurrentReviveInv,
                        inventory.GetItemAmountByType(ItemId.ItemRevive),
                        inventory.GetItemAmountByType(ItemId.ItemMaxRevive));
                    answerTextmessage += "\n";
                    answerTextmessage += _session.Translation.GetTranslation(TranslationString.CurrentMiscItemInv,
                        _session.Inventory.GetItemAmountByType(ItemId.ItemRazzBerry) +
                        _session.Inventory.GetItemAmountByType(ItemId.ItemBlukBerry) +
                        _session.Inventory.GetItemAmountByType(ItemId.ItemNanabBerry) +
                        _session.Inventory.GetItemAmountByType(ItemId.ItemWeparBerry) +
                        _session.Inventory.GetItemAmountByType(ItemId.ItemPinapBerry),
                        _session.Inventory.GetItemAmountByType(ItemId.ItemIncenseOrdinary) +
                        _session.Inventory.GetItemAmountByType(ItemId.ItemIncenseSpicy) +
                        _session.Inventory.GetItemAmountByType(ItemId.ItemIncenseCool) +
                        _session.Inventory.GetItemAmountByType(ItemId.ItemIncenseFloral),
                        _session.Inventory.GetItemAmountByType(ItemId.ItemLuckyEgg),
                        _session.Inventory.GetItemAmountByType(ItemId.ItemTroyDisk));
                    await SendMessage(message.Chat.Id, answerTextmessage);
                    break;

                case "/status":
                    answerTextmessage += Console.Title;

                    if (_session.LogicSettings.UseCatchLimit)
                    {
                        answerTextmessage += String.Format("\nCATCH LIMIT: {0}/{1}",
                                    _session.Stats.GetNumPokemonsInLast24Hours(),
                                    _session.LogicSettings.CatchPokemonLimit);
                    }

                    if (_session.LogicSettings.UsePokeStopLimit)
                    {
                        answerTextmessage += String.Format("\nPOKESTOP LIMIT: {0}/{1}",
                                    _session.Stats.GetNumPokestopsInLast24Hours(),
                                    _session.LogicSettings.PokeStopLimit);
                    }

                    await SendMessage(message.Chat.Id, answerTextmessage);
                    break;

                case "/restart":
                    Process.Start(Assembly.GetEntryAssembly().Location);
                    await SendMessage(message.Chat.Id, "Restarted Bot. Closing old Instance... BYE!");
                    Environment.Exit(-1);
                    break;

                case "/exit":
                    await SendMessage(message.Chat.Id, "Closing Bot... BYE!");
                    Environment.Exit(0);
                    break;

                case "/help":
                default:
                    answerTextmessage += _session.Translation.GetTranslation(TranslationString.HelpTemplate);
                    await SendMessage(message.Chat.Id, answerTextmessage);
                    break;
            }
        }

        private async void SendLocation(long chatId, double currentLatitude, double currentLongitude)
        {
            await _bot.SendLocationAsync(chatId, (float)currentLatitude, (float)currentLongitude);
        }

        private async Task SendMessage(long chatId, string message)
        {
            await _bot.SendTextMessageAsync(chatId, message, replyMarkup: new ReplyKeyboardHide());
        }
    }
}