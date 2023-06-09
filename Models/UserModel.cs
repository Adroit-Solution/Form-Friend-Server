﻿using System.ComponentModel.DataAnnotations;

namespace Server.Models
{
    public class UserModel
    {
        [Required]
        public string Email { get; set; }
        [Required]
        public string Password { get; set; }
        [Required]
        [Compare("Password")]
        public string ConfirmPassword { get; set; }
        [Required]
        public string UserName{ get; set; }
        [Required]
        public string Name { get; set; }
    }
}
