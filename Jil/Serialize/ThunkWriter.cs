﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jil.Serialize
{
    delegate string StringThunkDelegate<T>(ref ThunkWriter writer, T data, int depth);

    struct ThunkWriter
    {
        public string StaticToString()
        {
            throw new NotImplementedException();
        }
    }
}