﻿using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.Library;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using CommonIO;
using MediaBrowser.Controller.Providers;

namespace MediaBrowser.Controller.Entities
{
    /// <summary>
    /// Specialized folder that can have items added to it's children by external entities.
    /// Used for our RootFolder so plug-ins can add items.
    /// </summary>
    public class AggregateFolder : Folder
    {
        public AggregateFolder()
        {
            PhysicalLocationsList = new List<string>();
        }

        /// <summary>
        /// We don't support manual shortcuts
        /// </summary>
        protected override bool SupportsShortcutChildren
        {
            get
            {
                return false;
            }
        }

        public override bool CanDelete()
        {
            return false;
        }

        /// <summary>
        /// The _virtual children
        /// </summary>
        private readonly ConcurrentBag<BaseItem> _virtualChildren = new ConcurrentBag<BaseItem>();

        /// <summary>
        /// Gets the virtual children.
        /// </summary>
        /// <value>The virtual children.</value>
        public ConcurrentBag<BaseItem> VirtualChildren
        {
            get { return _virtualChildren; }
        }

        [IgnoreDataMember]
        public override IEnumerable<string> PhysicalLocations
        {
            get
            {
                return PhysicalLocationsList;
            }
        }

        public List<string> PhysicalLocationsList { get; set; }

        protected override IEnumerable<FileSystemMetadata> GetFileSystemChildren(IDirectoryService directoryService)
        {
            return CreateResolveArgs(directoryService, true).FileSystemChildren;
        }

        private bool _requiresRefresh;
        public override bool RequiresRefresh()
        {
            var changed = base.RequiresRefresh() || _requiresRefresh;

            if (!changed)
            {
                var locations = PhysicalLocations.ToList();

                var newLocations = CreateResolveArgs(new DirectoryService(Logger, FileSystem), false).PhysicalLocations.ToList();

                if (!locations.SequenceEqual(newLocations))
                {
                    changed = true;
                }
            }

            return changed;
        }

        public override bool BeforeMetadataRefresh()
        {
            var changed = base.BeforeMetadataRefresh() || _requiresRefresh;
            _requiresRefresh = false;
            return changed;
        }

        private ItemResolveArgs CreateResolveArgs(IDirectoryService directoryService, bool setPhysicalLocations)
        {
            var path = ContainingFolderPath;

            var args = new ItemResolveArgs(ConfigurationManager.ApplicationPaths , directoryService)
            {
                FileInfo = FileSystem.GetDirectoryInfo(path),
                Path = path,
                Parent = Parent
            };

            // Gather child folder and files
            if (args.IsDirectory)
            {
                var isPhysicalRoot = args.IsPhysicalRoot;

                // When resolving the root, we need it's grandchildren (children of user views)
                var flattenFolderDepth = isPhysicalRoot ? 2 : 0;

                var fileSystemDictionary = FileData.GetFilteredFileSystemEntries(directoryService, args.Path, FileSystem, Logger, args, flattenFolderDepth: flattenFolderDepth, resolveShortcuts: isPhysicalRoot || args.IsVf);

                // Need to remove subpaths that may have been resolved from shortcuts
                // Example: if \\server\movies exists, then strip out \\server\movies\action
                if (isPhysicalRoot)
                {
                    var paths = LibraryManager.NormalizeRootPathList(fileSystemDictionary.Values);

                    fileSystemDictionary = paths.ToDictionary(i => i.FullName);
                }

                args.FileSystemDictionary = fileSystemDictionary;
            }

            _requiresRefresh = _requiresRefresh || !args.PhysicalLocations.SequenceEqual(PhysicalLocations);
            if (setPhysicalLocations)
            {
                PhysicalLocationsList = args.PhysicalLocations.ToList();
            }

            return args;
        }
        
        /// <summary>
        /// Adds the virtual child.
        /// </summary>
        /// <param name="child">The child.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public void AddVirtualChild(BaseItem child)
        {
            if (child == null)
            {
                throw new ArgumentNullException();
            }

            _virtualChildren.Add(child);
        }

        /// <summary>
        /// Get the children of this folder from the actual file system
        /// </summary>
        /// <returns>IEnumerable{BaseItem}.</returns>
        protected override IEnumerable<BaseItem> GetNonCachedChildren(IDirectoryService directoryService)
        {
            return base.GetNonCachedChildren(directoryService).Concat(_virtualChildren);
        }

        /// <summary>
        /// Finds the virtual child.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>BaseItem.</returns>
        /// <exception cref="System.ArgumentNullException">id</exception>
        public BaseItem FindVirtualChild(Guid id)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentNullException("id");
            }

            return _virtualChildren.FirstOrDefault(i => i.Id == id);
        }
    }
}
