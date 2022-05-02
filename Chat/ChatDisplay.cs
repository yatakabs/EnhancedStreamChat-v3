﻿using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.FloatingScreen;
using BeatSaberMarkupLanguage.ViewControllers;
using BS_Utils.Utilities;
using EnhancedStreamChat.Configuration;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Interfaces;
using EnhancedStreamChat.Models;
using EnhancedStreamChat.Utilities;
using HMUI;
using IPA.Utilities;
using SiraUtil.Affinity;
using SiraUtil.Zenject;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRUIControls;
using Zenject;
using Color = UnityEngine.Color;

namespace EnhancedStreamChat.Chat
{
    [HotReload]
    public partial class ChatDisplay : BSMLAutomaticViewController, IAsyncInitializable, IChatDisplay, IDisposable, IAffinity
    {
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // プロパティ
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // イベント

        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // オーバーライドメソッド
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // パブリックメソッド
        public async Task InitializeAsync(CancellationToken token)
        {
            while (!this._fontManager.IsInitialized) {
                await Task.Delay(100);
            }
            this.SetupScreens();
            foreach (var msg in this._messages.ToArray()) {
                msg.Text.SetAllDirty();
                if (msg.SubTextEnabled) {
                    msg.SubText.SetAllDirty();
                }
            }
            (this.transform as RectTransform).pivot = new Vector2(0.5f, 0f);
            while (s_backupMessageQueue.TryDequeue(out var msg)) {
                await this.OnTextMessageReceived(msg.Value, msg.Key);
            }
            this._chatConfig.OnConfigChanged += this.Instance_OnConfigChanged;
            BSEvents.menuSceneActive += this.BSEvents_menuSceneActive;
            BSEvents.gameSceneActive += this.BSEvents_gameSceneActive;
            this._catCoreManager.OnJoinChannel += this.CatCoreManager_OnJoinChannel;
            this._catCoreManager.OnTwitchTextMessageReceived += this.CatCoreManager_OnTwitchTextMessageReceived;
            this._catCoreManager.OnMessageDeleted += this.OnCatCoreManager_OnMessageDeleted;
            this._catCoreManager.OnChatCleared += this.OnCatCoreManager_OnChatCleared;
            this._catCoreManager.OnFollow += this.OnCatCoreManager_OnFollow;
            this._catCoreManager.OnRewardRedeemed += this.OnCatCoreManager_OnRewardRedeemed;
        }

        public void OnMessageCleared(string messageId)
        {
            if (messageId != null) {
                MainThreadInvoker.Invoke(() =>
                {
                    foreach (var msg in this._messages.ToArray()) {
                        if (msg.Text.ChatMessage == null) {
                            continue;
                        }
                        if (msg.Text.ChatMessage.Id == messageId) {
                            this.ClearMessage(msg);
                        }
                    }
                });
            }
        }
        public void OnChatCleared(string userId)
        {
            MainThreadInvoker.Invoke(() =>
            {
                foreach (var msg in this._messages.ToArray()) {
                    if (msg.Text.ChatMessage == null) {
                        continue;
                    }
                    if (userId == null || msg.Text.ChatMessage.Sender.Id == userId) {
                        this.ClearMessage(msg);
                    }
                }
            });
        }

        public async Task OnTextMessageReceived(IESCChatMessage msg, DateTime dateTime)
        {
            var parsedMessage = await this._chatMessageBuilder.BuildMessage(msg, this._fontManager.FontInfo);
            HMMainThreadDispatcher.instance.Enqueue(() => this.CreateMessage(msg, dateTime, parsedMessage));
        }

        [AffinityPatch(typeof(VRPointer), nameof(VRPointer.OnEnable))]
        [AffinityPostfix]
        public void VRPointerOnEnable(VRPointer __instance)
        {
            this.PointerOnEnabled(__instance);
        }
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // プライベートメソッド
        private void AddMessage(EnhancedTextMeshProUGUIWithBackground newMsg)
        {
            newMsg.OnLatePreRenderRebuildComplete -= this.OnRenderRebuildComplete;
            newMsg.OnLatePreRenderRebuildComplete += this.OnRenderRebuildComplete;
            this.UpdateMessage(newMsg, true);
            this._messages.Enqueue(newMsg);
            this.ClearOldMessages();
        }
        private void PointerOnEnabled(VRPointer obj)
        {
            try {
                var mover = this._chatScreen.gameObject.GetComponent<FloatingScreenMoverPointer>();
                if (!mover) {
                    mover = this._chatScreen.gameObject.AddComponent<FloatingScreenMoverPointer>();
                    Destroy(this._chatScreen.screenMover);
                }
                this._chatScreen.screenMover = mover;
                this._chatScreen.screenMover.Init(this._chatScreen, obj);
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }
        private void SetupScreens()
        {
            if (this._chatScreen == null) {
                var screenSize = new Vector2(this.ChatWidth, this.ChatHeight);
                this._chatScreen = FloatingScreen.CreateFloatingScreen(screenSize, true, this.ChatPosition, Quaternion.identity, 0f, true);
                this._chatScreen.gameObject.layer = 5;
                var rectMask2D = this._chatScreen.GetComponent<RectMask2D>();
                if (rectMask2D) {
                    Destroy(rectMask2D);
                }

                this._chatContainer = new GameObject("chatContainer");
                this._chatContainer.transform.SetParent(this._chatScreen.transform, false);
                this._chatContainer.AddComponent<RectMask2D>().rectTransform.sizeDelta = screenSize;

                var canvas = this._chatScreen.GetComponent<Canvas>();
                canvas.worldCamera = Camera.main;
                canvas.sortingOrder = 3;

                this._chatScreen.SetRootViewController(this, AnimationType.None);
                this._rootGameObject = new GameObject();
                DontDestroyOnLoad(this._rootGameObject);

                this._chatMoverMaterial = Instantiate(BeatSaberUtils.UINoGlowMaterial);
                this._chatMoverMaterial.color = Color.clear;

                var renderer = this._chatScreen.handle.gameObject.GetComponent<Renderer>();
                renderer.material = this._chatMoverMaterial;
                renderer.material.mainTexture = this._chatMoverMaterial.mainTexture;

                this._chatScreen.transform.SetParent(this._rootGameObject.transform);
                this._chatScreen.ScreenRotation = Quaternion.Euler(this.ChatRotation);

                this._bg = this._chatScreen.GetComponentsInChildren<ImageView>().FirstOrDefault(x => x.name == "bg");
                this._bg.raycastTarget = false;
                this._bg.material = Instantiate(this._bg.material);
                this._bg.SetField("_gradient", false);
                this._bg.material.color = Color.white.ColorWithAlpha(1);
                this._bg.color = this.BackgroundColor;
                this._bg.SetAllDirty();
                this.AddToVRPointer();
                this.UpdateChatUI();
            }
        }

        private void Instance_OnConfigChanged()
        {
            this.UpdateChatUI();
        }

        private void OnHandleReleased(object sender, FloatingScreenHandleEventArgs e)
        {
            this.FloatingScreenOnRelease(e.Position, e.Rotation);
        }

        private void FloatingScreenOnRelease(in Vector3 pos, in Quaternion rot)
        {
            if (this._isInGame) {
                this._chatConfig.Song_ChatPosition = pos;
                this._chatConfig.Song_ChatRotation = rot.eulerAngles;
            }
            else {
                this._chatConfig.Menu_ChatPosition = pos;
                this._chatConfig.Menu_ChatRotation = rot.eulerAngles;
            }
        }

        private void BSEvents_gameSceneActive()
        {
            this._isInGame = true;
            foreach (var canvas in this._chatScreen.GetComponentsInChildren<Canvas>(true)) {
                canvas.sortingOrder = 0;
            }
            this.AddToVRPointer();
            this.UpdateChatUI();
        }

        private void BSEvents_menuSceneActive()
        {
            this._isInGame = false;
            foreach (var canvas in this._chatScreen.GetComponentsInChildren<Canvas>(true)) {
                canvas.sortingOrder = 3;
            }
            this.AddToVRPointer();
            this.UpdateChatUI();
        }

        private void AddToVRPointer()
        {
            if (this._chatScreen.screenMover) {
                this._chatScreen.HandleReleased -= this.OnHandleReleased;
                this._chatScreen.HandleReleased += this.OnHandleReleased;
                this._chatScreen.screenMover.transform.SetAsFirstSibling();
            }
        }
        private IEnumerator UpdateMessagePositions()
        {
            yield return this._waitForEndOfFrame;
            // TODO: Remove later on
            //float msgPos =  (ReverseChatOrder ?  ChatHeight : 0);
            float? msgPos = this.ChatHeight / (this.ReverseChatOrder ? 2f : -2f);
            foreach (var chatMsg in this._messages.OrderBy(x => x.ReceivedDate).Reverse()) {
                if (chatMsg == null) {
                    continue;
                }
                var msgHeight = (chatMsg.transform as RectTransform)?.sizeDelta.y;
                if (this.ReverseChatOrder) {
                    msgPos -= msgHeight;
                }
                chatMsg.transform.localPosition = new Vector3(0, msgPos ?? 0);
                if (!this.ReverseChatOrder) {
                    msgPos += msgHeight;
                }
            }
        }

        private void OnRenderRebuildComplete()
        {
            this._updateMessagePositions = true;
        }

        private void UpdateChatUI()
        {
            this.ChatWidth = this._chatConfig.ChatWidth;
            this.ChatHeight = this._chatConfig.ChatHeight;
            this.FontSize = this._chatConfig.FontSize;
            this.AccentColor = this._chatConfig.AccentColor;
            this.HighlightColor = this._chatConfig.HighlightColor;
            this.BackgroundColor = this._chatConfig.BackgroundColor;
            this.PingColor = this._chatConfig.PingColor;
            this.TextColor = this._chatConfig.TextColor;
            this.ReverseChatOrder = this._chatConfig.ReverseChatOrder;
            if (this._isInGame) {
                this.ChatPosition = this._chatConfig.Song_ChatPosition;
                this.ChatRotation = this._chatConfig.Song_ChatRotation;
            }
            else {
                this.ChatPosition = this._chatConfig.Menu_ChatPosition;
                this.ChatRotation = this._chatConfig.Menu_ChatRotation;
            }
            var chatContainerTransform = this._chatContainer.GetComponent<RectMask2D>().rectTransform!;
            chatContainerTransform.sizeDelta = new Vector2(this.ChatWidth, this.ChatHeight);

            this._chatScreen.handle.transform.localScale = new Vector3(this.ChatWidth, this.ChatHeight * 0.9f, 0.01f);
            this._chatScreen.handle.transform.localPosition = Vector3.zero;
            this._chatScreen.handle.transform.localRotation = Quaternion.identity;

            this.AllowMovement = this._chatConfig.AllowMovement;
            this.UpdateMessages();
        }

        private void UpdateMessages()
        {
            foreach (var msg in this._messages.ToArray()) {
                this.UpdateMessage(msg, true);
            }
            this._updateMessagePositions = true;
        }

        private void UpdateMessage(EnhancedTextMeshProUGUIWithBackground msg, bool setAllDirty = false)
        {
            (msg.transform as RectTransform).sizeDelta = new Vector2(this.ChatWidth, (msg.transform as RectTransform).sizeDelta.y);
            msg.Text.font = this._fontManager.MainFont;
            msg.Text.font.fallbackFontAssetTable = this._fontManager.FallBackFonts;
            msg.Text.overflowMode = TextOverflowModes.Overflow;
            msg.Text.alignment = TextAlignmentOptions.BottomLeft;
            msg.Text.color = this.TextColor;
            msg.Text.fontSize = this.FontSize;
            msg.Text.lineSpacing = 1.5f;

            msg.SubText.font = this._fontManager.MainFont;
            msg.SubText.font.fallbackFontAssetTable = this._fontManager.FallBackFonts;
            msg.SubText.overflowMode = TextOverflowModes.Overflow;
            msg.SubText.alignment = TextAlignmentOptions.BottomLeft;
            msg.SubText.color = this.TextColor;
            msg.SubText.fontSize = this.FontSize;
            msg.SubText.lineSpacing = 1.5f;

            if (msg.Text.ChatMessage != null) {
                msg.HighlightColor = this.HighlightColor;
                msg.AccentColor = this.AccentColor;
                msg.HighlightEnabled = msg.Text.ChatMessage.IsMentioned;
                msg.AccentEnabled = msg.HighlightEnabled || msg.SubText.ChatMessage != null;
            }

            if (setAllDirty) {
                msg.Text.SetAllDirty();
                if (msg.SubTextEnabled) {
                    msg.SubText.SetAllDirty();
                }
            }
        }
        private void ClearOldMessages()
        {
            while (this._messages.TryPeek(out var msg) && this.ReverseChatOrder ? msg.transform.localPosition.y < 0 - (msg.transform as RectTransform).sizeDelta.y : msg.transform.localPosition.y >= this._chatConfig.ChatHeight) {
                if (this._messages.TryDequeue(out msg)) {
                    this._textPoolContaner.Despawn(msg);
                }
            }
        }

        private string BuildClearedMessage(EnhancedTextMeshProUGUI msg)
        {
            var nameColorCode = msg.ChatMessage.Sender.Color;
            if (ColorUtility.TryParseHtmlString(msg.ChatMessage.Sender.Color.Substring(0, 7), out var nameColor)) {
                Color.RGBToHSV(nameColor, out var h, out var s, out var v);
                if (v < 0.85f) {
                    v = 0.85f;
                    nameColor = Color.HSVToRGB(h, s, v);
                }
                nameColorCode = ColorUtility.ToHtmlStringRGB(nameColor);
                nameColorCode = nameColorCode.Insert(0, "#");
            }
            var sb = new StringBuilder($"<color={nameColorCode}>{msg.ChatMessage.Sender.DisplayName}</color>");
            var badgeEndIndex = msg.text.IndexOf("<color=");
            if (badgeEndIndex != -1) {
                sb.Insert(0, msg.text.Substring(0, badgeEndIndex));
            }
            sb.Append(": <color=#bbbbbbbb><message deleted></color>");
            return sb.ToString();
        }

        private void ClearMessage(EnhancedTextMeshProUGUIWithBackground msg)
        {
            // Only clear non-system messages
            if (!msg.Text.ChatMessage.IsSystemMessage) {
                msg.Text.text = this.BuildClearedMessage(msg.Text);
                msg.SubTextEnabled = false;
            }
            if (msg.SubText.ChatMessage != null && !msg.SubText.ChatMessage.IsSystemMessage) {
                msg.SubText.text = this.BuildClearedMessage(msg.SubText);
            }
        }
        private void CreateMessage(IESCChatMessage msg, DateTime date, string parsedMessage)
        {
            if (this._lastMessage != null && !msg.IsSystemMessage && this._lastMessage.Text.ChatMessage.Id == msg.Id) {
                // If the last message received had the same id and isn't a system message, then this was a sub-message of the original and may need to be highlighted along with the original message
                this._lastMessage.SubText.text = parsedMessage;
                this._lastMessage.SubText.ChatMessage = msg;
                this._lastMessage.SubTextEnabled = true;
                this.UpdateMessage(this._lastMessage, true);
            }
            else {
                var newMsg = this._textPoolContaner.Spawn();
                newMsg.transform.SetParent(this._chatContainer.transform, false);
                this.UpdateMessage(newMsg);
                newMsg.gameObject.SetActive(true);
                newMsg.Text.ChatMessage = msg;
                newMsg.Text.text = parsedMessage;
                newMsg.ReceivedDate = date;
                this.AddMessage(newMsg);
                this._lastMessage = newMsg;
            }
            this._updateMessagePositions = true;
        }
        private void CatCoreManager_OnJoinChannel(CatCore.Services.Multiplexer.MultiplexedPlatformService arg1, CatCore.Services.Multiplexer.MultiplexedChannel arg2)
        {
            MainThreadInvoker.Invoke(() =>
            {
                var newMsg = this._textPoolContaner.Spawn();
                newMsg.transform.SetParent(this._chatContainer.transform, false);
                this.UpdateMessage(newMsg);
                newMsg.Text.text = $"<color=#bbbbbbbb>[{arg2.Name}] Success joining {arg2.Id}</color>";
                newMsg.HighlightEnabled = true;
                newMsg.HighlightColor = Color.gray.ColorWithAlpha(0.05f);
                this.AddMessage(newMsg);
            });
        }

        private void CatCoreManager_OnTwitchTextMessageReceived(CatCore.Services.Twitch.Interfaces.ITwitchService arg1, CatCore.Models.Twitch.IRC.TwitchMessage arg2)
        {
            _ = this.OnTextMessageReceived(new ESCChatMessage(arg2), DateTime.Now);
        }

        private void OnCatCoreManager_OnMessageDeleted(CatCore.Services.Multiplexer.MultiplexedPlatformService arg1, CatCore.Services.Multiplexer.MultiplexedChannel arg2, string arg3)
        {
            this.OnMessageCleared(arg3);
        }

        private void OnCatCoreManager_OnChatCleared(CatCore.Services.Multiplexer.MultiplexedPlatformService arg1, CatCore.Services.Multiplexer.MultiplexedChannel arg2, string arg3)
        {
            this.OnChatCleared(arg3);
        }

        private void OnCatCoreManager_OnFollow(string channelId, in CatCore.Models.Twitch.PubSub.Responses.Follow data)
        {
            var mes = new ESCChatMessage(Guid.NewGuid().ToString(), $"Thank you for following {data.DisplayName}({data.Username})!")
            {
                IsSystemMessage = true
            };
            _ = this.OnTextMessageReceived(mes, DateTime.Now);
        }
        private void OnCatCoreManager_OnRewardRedeemed(string channelId, in CatCore.Models.Twitch.PubSub.Responses.ChannelPointsChannelV1.RewardRedeemedData data)
        {
            var mes = new ESCChatMessage(Guid.NewGuid().ToString(), $"{data.User.DisplayName} used points {data.Reward.Title}({data.Reward.Cost}).")
            {
                IsSystemMessage = true
            };
            _ = this.OnTextMessageReceived(mes, DateTime.Now);
        }
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // メンバ変数
        private readonly ConcurrentQueue<EnhancedTextMeshProUGUIWithBackground> _messages = new ConcurrentQueue<EnhancedTextMeshProUGUIWithBackground>();
        private PluginConfig _chatConfig;
        private bool _isInGame;
        // TODO: eventually figure out a way to make this more modular incase we want to create multiple instances of ChatDisplay
        private static readonly ConcurrentQueue<KeyValuePair<DateTime, IESCChatMessage>> s_backupMessageQueue = new ConcurrentQueue<KeyValuePair<DateTime, IESCChatMessage>>();
        private FloatingScreen _chatScreen;
        private GameObject _chatContainer;
        private GameObject _rootGameObject;
        private Material _chatMoverMaterial;
        private ImageView _bg;
        private bool _updateMessagePositions = false;
        private readonly WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();
        private EnhancedTextMeshProUGUIWithBackground _lastMessage;
        private MemoryPoolContainer<EnhancedTextMeshProUGUIWithBackground> _textPoolContaner;
        private ICatCoreManager _catCoreManager;
        private ChatMessageBuilder _chatMessageBuilder;
        private ESCFontManager _fontManager;
        private bool _disposedValue;
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // 構築・破棄
        [Inject]
        public void Constarct(EnhancedTextMeshProUGUIWithBackground.Pool pool, PluginConfig config, ICatCoreManager catCoreManager, ChatMessageBuilder chatMessageBuilder, ESCFontManager fontManager)
        {
            this._textPoolContaner = new MemoryPoolContainer<EnhancedTextMeshProUGUIWithBackground>(pool);
            this._chatConfig = config;
            this._catCoreManager = catCoreManager;
            this._chatMessageBuilder = chatMessageBuilder;
            this._fontManager = fontManager;
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposedValue) {
                if (disposing) {
                    Destroy(this.gameObject);
                }
                this._disposedValue = true;
            }
        }
        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // Unity message
        protected void Awake()
        {
            DontDestroyOnLoad(this.gameObject);
        }

        protected override void OnDestroy()
        {
            this._chatConfig.OnConfigChanged -= this.Instance_OnConfigChanged;
            BSEvents.menuSceneActive -= this.BSEvents_menuSceneActive;
            BSEvents.gameSceneActive -= this.BSEvents_gameSceneActive;
            this._catCoreManager.OnJoinChannel -= this.CatCoreManager_OnJoinChannel;
            this._catCoreManager.OnTwitchTextMessageReceived -= this.CatCoreManager_OnTwitchTextMessageReceived;
            this._catCoreManager.OnMessageDeleted -= this.OnCatCoreManager_OnMessageDeleted;
            this._catCoreManager.OnChatCleared -= this.OnCatCoreManager_OnChatCleared;
            this._catCoreManager.OnFollow -= this.OnCatCoreManager_OnFollow;
            this._catCoreManager.OnRewardRedeemed -= this.OnCatCoreManager_OnRewardRedeemed;
            this.StopAllCoroutines();
            while (this._messages.TryDequeue(out var msg)) {
                msg.OnLatePreRenderRebuildComplete -= this.OnRenderRebuildComplete;
                if (msg.Text.ChatMessage != null) {
                    s_backupMessageQueue.Enqueue(new KeyValuePair<DateTime, IESCChatMessage>(msg.ReceivedDate, msg.Text.ChatMessage));
                }
                if (msg.SubText.ChatMessage != null) {
                    s_backupMessageQueue.Enqueue(new KeyValuePair<DateTime, IESCChatMessage>(msg.ReceivedDate, msg.SubText.ChatMessage));
                }
            }
            Destroy(this._rootGameObject);
            if (this._chatScreen != null) {
                Destroy(this._chatScreen);
                this._chatScreen = null;
            }
            if (this._chatMoverMaterial != null) {
                Destroy(this._chatMoverMaterial);
                this._chatMoverMaterial = null;
            }
            base.OnDestroy();
        }

        protected void Update()
        {
            if (!this._updateMessagePositions) {
                return;
            }
            HMMainThreadDispatcher.instance.Enqueue(this.UpdateMessagePositions());
            this._updateMessagePositions = false;
        }
        #endregion
    }
}