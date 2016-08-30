﻿using Microsoft.AspNetCore.Mvc.ModelBinding;
using Orchard.Body.Model;

namespace Orchard.Body.ViewModels
{
    public class BodyPartViewModel
    {
        public string Body { get; set; }

        [BindNever]
        public BodyPart BodyPart { get; set; }
    }
}
