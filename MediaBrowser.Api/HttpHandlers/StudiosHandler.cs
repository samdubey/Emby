﻿using System;
using MediaBrowser.Net.Handlers;
using MediaBrowser.Controller;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Api.HttpHandlers
{
    public class StudiosHandler : JsonHandler
    {
        protected sealed override object ObjectToSerialize
        {
            get
            {
                Folder parent = ApiService.GetItemById(QueryString["id"]) as Folder;
                Guid userId = Guid.Parse(QueryString["userid"]);

                return Kernel.Instance.GetAllStudios(parent, userId);
            }
        }
    }
}
