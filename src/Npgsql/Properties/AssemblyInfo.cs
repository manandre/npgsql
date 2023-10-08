﻿using System;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Security;

[assembly: CLSCompliant(false)]
[assembly: AllowPartiallyTrustedCallers]
[assembly: AssemblyTrademark("")]
[assembly: SecurityRules(SecurityRuleSet.Level1)]

#if NET5_0_OR_GREATER
[module: SkipLocalsInit]
#endif

[assembly: InternalsVisibleTo("EntityFramework6.Npgsql, PublicKey=" +
"0024000004800000940000000602000000240000525341310004000001000100" +
"2b3c590b2a4e3d347e6878dc0ff4d21eb056a50420250c6617044330701d35c9" +
"8078a5df97a62d83c9a2db2d072523a8fc491398254c6b89329b8c1dcef43a1e" +
"7aa16153bcea2ae9a471145624826f60d7c8e71cd025b554a0177bd935a78096" +
"29f0a7afc778ebb4ad033e1bf512c1a9c6ceea26b077bc46cac93800435e77ee")]

[assembly: InternalsVisibleTo("EntityFramework5.Npgsql, PublicKey=" +
"0024000004800000940000000602000000240000525341310004000001000100" +
"2b3c590b2a4e3d347e6878dc0ff4d21eb056a50420250c6617044330701d35c9" +
"8078a5df97a62d83c9a2db2d072523a8fc491398254c6b89329b8c1dcef43a1e" +
"7aa16153bcea2ae9a471145624826f60d7c8e71cd025b554a0177bd935a78096" +
"29f0a7afc778ebb4ad033e1bf512c1a9c6ceea26b077bc46cac93800435e77ee")]

[assembly: InternalsVisibleTo("Npgsql.Tests, PublicKey=" +
"0024000004800000940000000602000000240000525341310004000001000100" +
"2b3c590b2a4e3d347e6878dc0ff4d21eb056a50420250c6617044330701d35c9" +
"8078a5df97a62d83c9a2db2d072523a8fc491398254c6b89329b8c1dcef43a1e" +
"7aa16153bcea2ae9a471145624826f60d7c8e71cd025b554a0177bd935a78096" +
"29f0a7afc778ebb4ad033e1bf512c1a9c6ceea26b077bc46cac93800435e77ee")]

[assembly: InternalsVisibleTo("Npgsql.PluginTests, PublicKey=" +
"0024000004800000940000000602000000240000525341310004000001000100" +
"2b3c590b2a4e3d347e6878dc0ff4d21eb056a50420250c6617044330701d35c9" +
"8078a5df97a62d83c9a2db2d072523a8fc491398254c6b89329b8c1dcef43a1e" +
"7aa16153bcea2ae9a471145624826f60d7c8e71cd025b554a0177bd935a78096" +
"29f0a7afc778ebb4ad033e1bf512c1a9c6ceea26b077bc46cac93800435e77ee")]

[assembly: InternalsVisibleTo("Npgsql.Benchmarks, PublicKey=" +
"0024000004800000940000000602000000240000525341310004000001000100" +
"2b3c590b2a4e3d347e6878dc0ff4d21eb056a50420250c6617044330701d35c9" +
"8078a5df97a62d83c9a2db2d072523a8fc491398254c6b89329b8c1dcef43a1e" +
"7aa16153bcea2ae9a471145624826f60d7c8e71cd025b554a0177bd935a78096" +
"29f0a7afc778ebb4ad033e1bf512c1a9c6ceea26b077bc46cac93800435e77ee")]

[assembly: InternalsVisibleTo("Npgsql.DependencyInjection, PublicKey=" +
"0024000004800000940000000602000000240000525341310004000001000100" +
"2b3c590b2a4e3d347e6878dc0ff4d21eb056a50420250c6617044330701d35c9" +
"8078a5df97a62d83c9a2db2d072523a8fc491398254c6b89329b8c1dcef43a1e" +
"7aa16153bcea2ae9a471145624826f60d7c8e71cd025b554a0177bd935a78096" +
"29f0a7afc778ebb4ad033e1bf512c1a9c6ceea26b077bc46cac93800435e77ee")]
