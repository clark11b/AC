namespace ACE.Network.GameAction
{
    public enum GameActionType
    {
        SetSingleCharacterOption             = 0x0005,
        TargetedMeleeAttack                  = 0x0008,
        TargetedMissileAttack                = 0x000A,
        SetAfkMode                           = 0x000F,
        SetAfkMessage                        = 0x0010,
        Talk                                 = 0x0015,
        RemoveFriend                         = 0x0017,
        AddFriend                            = 0x0018,
        PutItemInContainer                   = 0x0019,
        GetAndWieldItem                      = 0x001A,
        DropItem                             = 0x001B,
        SwearAllegiance                      = 0x001D,
        BreakAllegiance                      = 0x001E,
        AllegianceUpdateRequest              = 0x001F,
        RemoveAllFriends                     = 0x0025,
        TeleToPklArena                       = 0x0026,
        TeleToPkArena                        = 0x0027,
        TitleSet                             = 0x002C,
        QueryAllegianceName                  = 0x0030,
        ClearAllegianceName                  = 0x0031,
        TalkDirect                           = 0x0032,
        SetAllegianceName                    = 0x0033,
        UseWithTarget                        = 0x0035,
        Use                                  = 0x0036,
        SetAllegianceOfficer                 = 0x003B,
        SetAllegianceOfficerTitle            = 0x003C,
        ListAllegianceOfficerTitles          = 0x003D,
        ClearAllegianceOfficerTitles         = 0x003E,
        DoAllegianceLockAction               = 0x003F,
        SetAllegianceApprovedVassal          = 0x0040,
        AllegianceChatGag                    = 0x0041,
        DoAllegianceHouseAction              = 0x0042,
        RaiseVital                           = 0x0044,
        RaiseAbility                         = 0x0045,
        RaiseSkill                           = 0x0046,
        TrainSkill                           = 0x0047,
        CastUntargetedSpell                  = 0x0048,
        CastTargetedSpell                    = 0x004A,
        ChangeCombatMode                     = 0x0053,
        StackableMerge                       = 0x0054,
        StackableSplitToContainer            = 0x0055,
        StackableSplitTo3D                   = 0x0056,
        ModifyCharacterSquelch               = 0x0058,
        ModifyAccountSquelch                 = 0x0059,
        ModifyGlobalSquelch                  = 0x005B,
        Tell                                 = 0x005D,
        Buy                                  = 0x005F,
        Sell                                 = 0x0060,
        TeleToLifestone                      = 0x0063,
        LoginComplete                        = 0x00A1,
        FellowshipCreate                     = 0x00A2,
        FellowshipQuit                       = 0x00A3,
        FellowshipDismiss                    = 0x00A4,
        FellowshipRecruit                    = 0x00A5,
        FellowshipUpdateRequest              = 0x00A6,
        BookData                             = 0x00AA,
        BookModifyPage                       = 0x00AB,
        BookAddPage                          = 0x00AC,
        BookDeletePage                       = 0x00AD,
        BookPageData                         = 0x00AE,
        SetInscription                       = 0x00BF,
        IdentifyObject                       = 0x00C8,
        GiveObjectRequest                    = 0x00CD,
        AdvocateTeleport                     = 0x00D6,
        AbuseLogRequest                      = 0x0140,
        AddChannel                           = 0x0145,
        RemoveChannel                        = 0x0146,
        ChatChannel                          = 0x0147,
        ListChannels                         = 0x0148,
        IndexChannels                        = 0x0149,
        NoLongerViewingContents              = 0x0195,
        StackableSplitToWield                = 0x019B,
        AddShortCut                          = 0x019C,
        RemoveShortCut                       = 0x019D,
        SetCharacterOptions                  = 0x01A1,
        RemoveSpellC2S                       = 0x01A8,
        CancelAttack                         = 0x01B7,
        QueryHealth                          = 0x01BF,
        QueryAge                             = 0x01C2,
        QueryBirth                           = 0x01C4,
        Emote                                = 0x01DF,
        SoulEmote                            = 0x01E1,
        AddSpellFavorite                     = 0x01E3,
        RemoveSpellFavorite                  = 0x01E4,
        PingRequest                          = 0x01E9,
        OpenTradeNegotiations                = 0x01F6,
        CloseTradeNegotiations               = 0x01F7,
        AddToTrade                           = 0x01F8,
        AcceptTrade                          = 0x01FA,
        DeclineTrade                         = 0x01FB,
        ResetTrade                           = 0x0204,
        ClearPlayerConsentList               = 0x0216,
        DisplayPlayerConsentList             = 0x0217,
        CharacterRemoveFromPlayerConsentList = 0x0218,
        RemoveFromPlayerConsentList          = 0x0218,
        CharacterAddPlayerPermission         = 0x0219,
        RemovePlayerPermission               = 0x021A,
        BuyHouse                             = 0x021C,
        HouseQuery                           = 0x021E,
        AbandonHouse                         = 0x021F,
        RentHouse                            = 0x0221,
        SetDesiredComponentLevel             = 0x0224,
        AddPermanentGuest                    = 0x0245,
        RemovePermanentGuest                 = 0x0246,
        SetOpenHouseStatus                   = 0x0247,
        ChangeStoragePermission              = 0x0249,
        BootSpecificHouseGuest               = 0x024A,
        RemoveAllStoragePermission           = 0x024C,
        RequestFullGuestList                 = 0x024D,
        SetMotd                              = 0x0254,
        QueryMotd                            = 0x0255,
        ClearMotd                            = 0x0256,
        QueryLord                            = 0x0258,
        AddAllStoragePermission              = 0x025C,
        RemoveAllPermanentGuests             = 0x025E,
        BootEveryone                         = 0x025F,
        TeleToHouse                          = 0x0262,
        QueryItemMana                        = 0x0263,
        SetHooksVisibility                   = 0x0266,
        ModifyAllegianceGuestPermission      = 0x0267,
        ModifyAllegianceStoragePermission    = 0x0268,
        Join                                 = 0x0269,
        Quit2                                = 0x026A,
        Move                                 = 0x026B,
        MovePass                             = 0x026D,
        Stalemate                            = 0x026E,
        ListAvailableHouses                  = 0x0270,
        ConfirmationResponse                 = 0x0275,
        BreakAllegianceBoot                  = 0x0277,
        TeleToMansion                        = 0x0278,
        Suicide                              = 0x0279,
        AllegianceInfoRequest                = 0x027B,
        CreateTinkeringTool                  = 0x027D,
        SpellbookFilter                      = 0x0286,
        TeleToMarketPlace                    = 0x028D,
        EnterPkLite                          = 0x028F,
        FellowshipAssignNewLeader            = 0x0290,
        FellowshipChangeOpenness             = 0x0291,
        AllegianceChatBoot                   = 0x02A0,
        AddAllegianceBan                     = 0x02A1,
        RemoveAllegianceBan                  = 0x02A2,
        ListAllegianceBans                   = 0x02A3,
        RemoveAllegianceOfficer              = 0x02A5,
        ListAllegianceOfficers               = 0x02A6,
        ClearAllegianceOfficers              = 0x02A7,
        RecallAllegianceHometown             = 0x02AB,
        QueryPluginListResponse              = 0x02AF,
        QueryPluginResponse                  = 0x02B2,
        FinishBarber                         = 0x0311,
        AbandonContract                      = 0x0316,
        Jump                                 = 0xF61B,
        MoveToState                          = 0xF61C,
        DoMovementCommand                    = 0xF61E,
        TurnTo                               = 0xF649,
        StopMovementCommand                  = 0xF661,
        ForceObjectDescSend                  = 0xF6EA,
        ObjectCreate                         = 0xF745,
        ObjectDelete                         = 0xF747,
        MovementEvent                        = 0xF74C,
        ApplySoundEffect                     = 0xF750,
        AutonomyLevel                        = 0xF752,
        AutonomousPosition                   = 0xF753,
        ApplyVisualEffect                    = 0xF755,
        JumpNonAutonomous                    = 0xF7C9,
    }
}
