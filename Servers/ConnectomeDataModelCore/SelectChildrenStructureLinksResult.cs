﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.DataModel.Annotation
{
    public partial class SelectChildrenStructureLinksResult
    {
        public long SourceID { get; set; }
        public long TargetID { get; set; }
        public bool Bidirectional { get; set; }
        public string Tags { get; set; }
        public string Username { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }
    }
}
