﻿using Newtonsoft.Json;

namespace UnoraLaunchpad
{
    public class Character
    {
        public string Username { get; set; }
        [JsonIgnore]
        public string Password { get; set; }
        public string EncryptedPassword { get; set; }
    }
}