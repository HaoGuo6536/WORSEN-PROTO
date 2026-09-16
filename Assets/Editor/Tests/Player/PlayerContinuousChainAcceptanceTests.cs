// ============================================================================
// PlayerContinuousChainAcceptanceTests.cs
// ============================================================================
// PURPOSE:
//   Captures three prospectively fixed continuous movement chains using real
//   collision results and the shipped movement tuning. Collection integrity,
//   ordered completion, pooled speed and measured locks have separate verdicts.
//   Revision 023 seals journals before hashing and tolerates float representation
//   at the sole sprint-to-slide admission; original course/acceptance stay fixed.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Player physical integration.
// KEY RESPONSIBILITIES:
//   - Retain every attempted tick and transition, including stalled/failed runs.
//   - Preserve native recordings and verify exact recorded-resolution replay.
//   - Keep original M1 thresholds separate from Explicit NUnit capture integrity.
// DEPENDENCIES:
//   Core; Player/Level/Hunter/Chase; Run/Input/Telemetry; actual TagArena factory.
//   Unity physics, Input System, UnityEditor, NUnit and Unity Test Framework.
// USAGE NOTES:
//   Coordinator holds the Unity lease. Only temporary course geometry and the
//   initial factory spawn are arranged; no runtime pose/velocity/tuning writes.
//   Supplied nonzero input replaces an isolated neutral hardware producer.
//   This arranged aerial vault endpoint is not an authored Floor route, chase,
//   hardware-input, human-readability or deterministic-physics acceptance claim.
//   Finally restores subscriptions, device filter and background state; normal
//   scene unload and Test Framework restore temporary geometry/editor scenes.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Chase;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Orchestrator;
using Worsen.Presentation.Input;
using Worsen.Presentation.Telemetry;
using Worsen.Session.Run;

namespace Worsen.Tests.Player
{
    public sealed class PlayerContinuousChainAcceptanceTests
    {
        private const string Arena = "Assets/Scenes/TagArena.unity";
        private const string Fixture = "Assets/Editor/Tests/Player/PlayerContinuousChainAcceptanceTests.cs";
        private const string ProtocolSha256 = "D4F0F6D9DBD9E16830DFE2725997E84E28321979B7C15AADF77AF8CD67422CB5";
        private const string ProtocolBase64 = "IyBDb250aW51b3VzIGNoYWluIDAyMyDigJQgcHJvc3BlY3RpdmUgY29sbGVjdGlvbiBwcm90b2NvbCB2MgoKVGhpcyBpcyBhIG5ldywgZXhwbGljaXRseSBhcnJhbmdlZCBlbXB0eS1yb29tIE0xIGZpeHR1cmUuIEl0IG1lYXN1cmVzIHRoZSBvcmlnaW5hbCBzcHJpbnQg4oaSIHNsaWRlIOKGkiBzbGlkZS1qdW1wIOKGkiB2YXVsdCDihpIgcmVib3VuZCDihpIgbGFuZCBpbnRvIHNwcmludCBjaGFpbiB3aXRoIHRoZSBzaGlwcGVkIFBsYXllciBwcm9maWxlIGFuZCBtb3ZlciBjb25maWcuIEl0IGlzIG5vdCBhbiBhdXRob3JlZCBGbG9vckxvb3AvVGFnQXJlbmEgcm91dGUsIGEgY2hhc2UsIHBoeXNpY2FsLWRldmljZSBpbnB1dCwgaHVtYW4gcmVhZGFiaWxpdHksIG9yIGNvbWZvcnQgYWNjZXB0YW5jZS4KCiMjIEZyb3plbiBzYW1wbGUgYW5kIHN0b3BwaW5nIHJ1bGVzCgotIE9uZSBOVW5pdCBgRXhwbGljaXRgIFVuaXR5IHRlc3QgY29sbGVjdHMgZXhhY3RseSAqKnRocmVlIGZyZXNoIHNjZW5lL3BsYXllciBydW5zKiosIGVhY2ggc2VlZCAqKjEqKi4gQSBmcmVzaCBzY2VuZSBmYWN0b3J5IGFuZCBSdW4gUHJlcGFyZVNjZW5lIHJlc2V0IGVzdGFibGlzaCBlYWNoIGluaXRpYWwgc3RhdGU7IHRoZSBjYW5vbmljYWwgc2VydmljZSBtYXkgcGVyc2lzdCB3aXRoIGl0cyB1bmNoYW5nZWQgc2VlZC4KLSBNb3Rpb24gaXMgcmVxdWVzdGVkIG9uIHRoZSBmaXJzdCBjb21taXR0ZWQgdGljay4gRXZlcnkgY29tbWl0dGVkIHRpY2sgZnJvbSB0aGF0IGluaXRpYWwgaW5wdXQgdGhyb3VnaCB0ZXJtaW5hdGlvbiBpcyByZXRhaW5lZCwgaW5jbHVkaW5nIGFjY2VsZXJhdGlvbiwgYmxvY2tlZCBtb3ZlbWVudCwgbG93IHNwZWVkcywgbGFuZGluZ3MsIGZhaWx1cmUgcGVyaW9kcywgYW5kIHRoZSB0ZXJtaW5hdGluZyB0aWNrLgotIEVhY2ggcnVuIGVuZHMgYXQgdGhlICoqZmlyc3QgY3Jvc3Npbmcgb2YgdGhlIHJldHVybiBmaW5pc2ggcGxhbmUgeCA9IC0zKiosIG9yICoqMSw4MDAgY29tbWl0dGVkIHRpY2tzKiosIHdoaWNoZXZlciBvY2N1cnMgZmlyc3QuIENvb3JkaW5hdGVzIGJlbG93IGFyZSByZWxhdGl2ZSB0byB0aGUgY291cnNlIG9yaWdpbi4gRmluaXNoIGNyb3NzaW5nIGRvZXMgbm90IGRlcGVuZCBvbiBzdWNjZXNzZnVsIHRyYXZlcnNhbCwgZ3JvdW5kZWQgc3RhdGUsIHNwZWVkLCBvciBhIHBhc3NpbmcgbWV0cmljOyB0aGUgcnVuIG5ldmVyIHdhaXRzIGJleW9uZCB0aGUgbGluZSB0byBpbXByb3ZlIGl0cyByZXN1bHRzLgotIEFsbCB0aHJlZSBydW5zIGFyZSBjb2xsZWN0ZWQgZXZlbiBpZiBjaGFpbiBvcmRlci9jb21wbGV0aW9uLCBwOTAgc3BlZWQsIG9yIGlucHV0LWxvY2sgYWNjZXB0YW5jZSBmYWlscy4gT25seSBhIHJlYWwgY29sbGVjdGlvbi1pbnRlZ3JpdHkgYnJlYWsgKHNvdXJjZS9jb25maWcgZHJpZnQsIHVuZXhwZWN0ZWQgaW5wdXQvdGljayBzZXF1ZW5jZSwgcmVjb3JkZXIvcmVwbGF5IGZhaWx1cmUsIG1pc3NpbmcgaW5pdGlhbCBzdGF0ZSwgbG9zdCBvYnNlcnZhdGlvbiwgb3IgYSA5MC1zZWNvbmQgd2FsbC1jbG9jayBmYWlsdXJlIHRvIGZpbmlzaCB0aGUgYm91bmRlZCBjb21taXR0ZWQtdGljayB3aW5kb3cpIHN0b3BzIHRoZSByZW1haW5pbmcgcnVucy4gUGFydGlhbC9pbnRlcnJ1cHRlZCBhdHRlbXB0cyByZW1haW4gcmVzZXJ2ZWQgaW4gdGhlIGNvaG9ydCByZXBvcnQgYW5kIHJldGFpbiBldmVyeSBhdmFpbGFibGUgcmF3IHJlY29yZC4KLSBUaGUgbm9ybWFsIHN1aXRlIHNraXBzIHRoaXMgRXhwbGljaXQgY2FzZS4gTmF0aXZlIGdyZWVuIG1lYW5zICoqY29sbGVjdGlvbiBpbnRlZ3JpdHkgb25seSoqLiBUaGUgSlNPTiByZXBvcnQgc2VwYXJhdGVseSBkZWNsYXJlcyBjb2xsZWN0aW9uIGludGVncml0eSwgY2hhaW4gYWNjZXB0YW5jZSwgcDkwIGFjY2VwdGFuY2UsIGlucHV0LWxvY2sgYWNjZXB0YW5jZSwgYW5kIHRoZWlyIGNvbWJpbmVkIGFjY2VwdGFuY2UuIE5vIHBhc3NpbmcgdmVyZGljdCBpcyBpbmZlcnJlZCBmcm9tIHRoZSB0ZXN0IHJ1bm5lciBhbG9uZS4KCiMjIEZyb3plbiBwaHlzaWNhbCBjb3Vyc2UKCk9yaWdpbiBpcyBgKDIwMDAsIDAsIDApYCBpbiB0aGUgbG9hZGVkIHNjZW5lOyBhbGwgZm9sbG93aW5nIHBvc2l0aW9ucyBhcmUgcmVsYXRpdmUuIFRoZXNlIGFyZSB0ZW1wb3JhcnkgdGVzdCBnZW9tZXRyeSBjcmVhdGVkIGJlZm9yZSB0aGUgc2NlbmUgZmFjdG9yeSBzcGF3bnMgdGhlIFBsYXllciBhbmQgZGVzdHJveWVkIGJ5IG5vcm1hbCBzY2VuZSB1bmxvYWQuCgp8IEVsZW1lbnQgfCBQbGFjZW1lbnQgfAp8LS0tfC0tLXwKfCBQbGF5ZXIgfCBgKDAsIDAuMDUsIDApYCwgaGVhZGluZyA5MMKwIC8gZWFzdCwgdmlhIHRoZSBzY2VuZSdzIGFjdHVhbCBQbGF5ZXJGYWN0b3J5OyBubyBsYXRlciBwb3NlIG9yIHZlbG9jaXR5IHdyaXRlcyB8CnwgRmxvb3IgfCB4IGBbLTEyLCAyMF1gLCB6IGBbLTYsIDZdYCwgdG9wIHkgYDBgLCB0aGlja25lc3MgYDAuNWAgfAp8IFZhdWx0IGJsb2NrIHwgeCBgWzYsIDYuNzVdYCwgeiBgWy0yLCAyXWAsIGhlaWdodCBgMS4yYCB8CnwgVmF1bHQgbWV0YWRhdGEgfCBJRCBgOTQwMDFgLCBWYXVsdFN1cmZhY2UsIGV4cGxpY2l0IGZhci1zaWRlIGFlcmlhbCBlbmRwb2ludCBgKDcuMSwgMS4yLCAwKWAgfAp8IFJlYm91bmQgd2FsbCB8IEZyb250IHggYDguMDVgLCB0aGlja25lc3MgYDAuNWAsIHogYFstMywgM11gLCB5IGBbMCwgNl1gLCBJRCBgOTQwMDJgIC8gUmVib3VuZFN1cmZhY2UgfAp8IEZpbmlzaCBwbGFuZSB8IFJldHVybiBjcm9zc2luZyBhdCB4IGAtM2A7IG5vIHRyaWdnZXIsIG9ubHkgb2JzZXJ2YXRpb24gb2YgcmVzb2x2ZWQgcG9zaXRpb24gfAoKVGhlIHZhdWx0IGVuZHBvaW50IGlzIGludGVudGlvbmFsbHkgYW4gYWVyaWFsIGZhci1zaWRlIHRyYXZlcnNhbCBlbmRwb2ludCwgd2l0aCBgMC4zNSBtYCBjbGVhcmFuY2UgcGFzdCB0aGUgYmxvY2sgZm9yIHRoZSBkZWZhdWx0IGAwLjMgbWAgcmFkaXVzIGNhcHN1bGUuIEl0IHByZXNlcnZlcyBhbiBhaXJib3JuZSBhcHByb2FjaCB0byB0aGUgcmVib3VuZCB3YWxsIHVzaW5nIHRoZSBub3JtYWwgY29sbGlzaW9uLWNoZWNrZWQgdmF1bHQgYXJjLiBObyBzY3JpcHRlZCBtb3Rpb24gb3IgZmFrZSBwcm9iZS9yZXNvbHV0aW9uIG1vdmVzIHRoZSBQbGF5ZXIgdG8gaXQuIE5hdGl2ZSBjb2xsaXNpb24gYW5kIGNvbXBsZXRpb24gZmFjdHMgbXVzdCB2ZXJpZnkgdGhpcyBwcm9zcGVjdGl2ZSBnZW9tZXRyeTsgc3RhdGljIGFyaXRobWV0aWMgaXMgbm90IGEgcGh5c2ljYWwgcGFzcy4KCkh1bnRlcnMgaW4gdGhlIG9yZGluYXJ5IHNjZW5lIGFyZSBkaXNhYmxlZCBiZWZvcmUgdGhlIGZpcnN0IHRpY2ssIGFuZCBubyBGbG9vci9EaXJlY3RvciBzaW11bGF0aW9uIGlzIGJvdW5kIGluIHRoaXMgVGFnQXJlbmEgZml4dHVyZS4gU2hpcHBlZCBtb3ZlbWVudCwgaGVhbHRoLCBjYW1lcmEsIHBoeXNpY3MsIHByb2ZpbGUvY29uZmlnIGFuZCBwcmVmYWIgdmFsdWVzIGFyZSBuZXZlciBjaGFuZ2VkLiBJbml0aWFsIHNjZW5lIHNlZWQvc3Bhd24gYW5kIHRlbXBvcmFyeSBjb3Vyc2UgZ2VvbWV0cnkgYXJlIHRoZSBvbmx5IGFycmFuZ2VtZW50IGNoYW5nZXMuCgojIyBGcm96ZW4gaW5wdXQgcG9saWN5CgoxLiBSZXF1ZXN0IGZ1bGwgZWFzdHdhcmQgbW92ZW1lbnQgZnJvbSB0aGUgZmlyc3QgY29tbWl0dGVkIHRpY2suIE5ldmVyIGhvbGQgdGhlIHByZWNpc2lvbi13YWxrIGBTcHJpbnRgIGJ1dHRvbiwgbmV2ZXIgc2V0IG1vdmUgaW5wdXQgdG8gemVybyBiZWZvcmUgdGVybWluYXRpb24sIGFuZCBuZXZlciBzdG9wIGF0IGEgd2F5cG9pbnQuCjIuIEFmdGVyIHJlc29sdmVkIHggaXMgYXQgbGVhc3QgYDEgbWAsIHJlcXVlc3QgdGhlIHNvbGUgY3JvdWNoIHByZXNzIG9uY2UgYWN0dWFsIGhvcml6b250YWwgZGVjaXNpb24gdmVsb2NpdHkgaXMgYXQgbGVhc3QgYFNwcmludFNwZWVkIC0gMC4wMDAwMSBtL3NgLiBUaGlzIHJlcHJlc2VudGF0aW9uIHRvbGVyYW5jZSBpcyBmaXhlZCBiZWZvcmUgY29sbGVjdGlvbiBhbmQgbWF0Y2hlcyB0aGUgZXhpc3RpbmcgZmluYWwgc3ByaW50IHByZWRpY2F0ZS4gVGhpcyBhZG1pdHMgb25lIHNsaWRlIGJvb3N0LiBEbyBub3QgcmV0cnkgdGhlIHByZXNzIG9yIHN0YXJ0IGFub3RoZXIgc2xpZGUuCjMuIEFmdGVyIHNpeCBvYnNlcnZlZCBTbGlkZSB0aWNrcywgcmVsZWFzZSBjcm91Y2ggYW5kIHJlcXVlc3Qgb25lIGp1bXAgZWRnZS4gVGhlIHJvdXRlIG11c3QgcHJvZHVjZSBhbiBhY3R1YWwgSnVtcCBmYWN0IHdoaWxlIGxlYXZpbmcgU2xpZGU7IGEgdmF1bHQgc3Vic3RpdHV0ZWQgZm9yIHRoYXQganVtcCBmYWlscyB0aGUgZGVjbGFyZWQgb3JkZXIuCjQuIEFmdGVyIHRoYXQgYWN0dWFsIHNsaWRlLWp1bXAgZmFjdCwgcmVxdWVzdCB0aGUgc29sZSB2YXVsdCBqdW1wIGVkZ2Ugb25jZSB0aGUgYWN0dWFsIHByb2JlIHJlcG9ydHMgVmF1bHRDYW5kaWRhdGUsIGNsZWFyYW5jZSwgaGVpZ2h0IHdpdGhpbiB0aGUgc2hpcHBlZCB2YXVsdCByYW5nZSBgW1ZhdWx0TWluaW11bUhlaWdodCwgVmF1bHRNYXhpbXVtSGVpZ2h0XWAsIGFuZCBubyBzdGFuZGluZyBibG9ja2FnZS4gSWYgYSBqdW1wIGlzIHN0aWxsIGhlbGQsIHJlbGVhc2UgaXQgZm9yIGEgdGljayBiZWZvcmUgdGhlIG5leHQgZWRnZS4gTmV2ZXIgdGVsZXBvcnQsIGNoYW5nZSB2ZWxvY2l0eSwgb3IgZm9yY2UgYSB0cmF2ZXJzYWwgc3RhdGUuCjUuIEFmdGVyIG9uZSBzdWNjZXNzZnVsIFZhdWx0IGNvbXBsZXRpb24gZmFjdCwgcmVxdWVzdCB0aGUgcmVib3VuZCBqdW1wIGVkZ2Ugb25jZSBhY3R1YWwgc3RhdGUgaXMgQWlyIGFuZCB0aGUgYWN0dWFsIHByb2JlIGhhcyB3YWxsIElEIGA5NDAwMmAsIGRpc3RhbmNlIHdpdGhpbiBgUmVib3VuZERpc3RhbmNlYCwgYW5kIGFuZ2xlIHdpdGhpbiBgUmVib3VuZEFuZ2xlYC4gTm8gcmVwZWF0ZWQgcmVib3VuZCBwcmVzc2VzIG9yIHNhbWUtd2FsbCBjaGFpbiBpcyBhbGxvd2VkLgo2LiBBZnRlciBhbiBhY3R1YWwgc3VjY2Vzc2Z1bCBSZWJvdW5kIGZhY3QsIHJlcXVlc3QgZnVsbCB3ZXN0d2FyZCB0cmF2ZWwgdXNpbmcgb3JkaW5hcnkgYm9keS1yZWxhdGl2ZSBtb3ZlIGF4ZXMuIFR1cm4gYm9keSBsb29rIHRvd2FyZCBoZWFkaW5nIDI3MMKwIGJ5IGF0IG1vc3QgKioxNcKwIHBlciBjb21taXR0ZWQgdGljayoqLiBUaGlzIGtlZXBzIGlucHV0IG5vbnplcm8gd2hpbGUgYWxsb3dpbmcgdGhlIHBoeXNpY2FsIHJlZmxlY3RlZCB0cmFqZWN0b3J5IHRvIGNvbnRpbnVlLiBCZWZvcmUgcmVib3VuZCwgZnVsbCBlYXN0d2FyZCBtb3ZlbWVudCBjb250aW51ZXMgdGhyb3VnaCB0aGUgdmF1bHQgYW5kIGFueSBmYWlsZWQvc3RhbGxlZCBhcHByb2FjaC4KNy4gT25jZSB0aGUgZmlyc3QgZmluaXNoIGNyb3NzaW5nIG9yIHRpY2sgbGltaXQgaXMgb2JzZXJ2ZWQsIGNsb3NlIHRoZSBpbnRlcnZhbCBhdCB0aGF0IGV4YWN0IGNvbW1pdHRlZCB0aWNrLiBOZXV0cmFsIHRlYXJkb3duIGlucHV0IGlzIG91dHNpZGUgdGhlIG1lYXN1cmVkIGludGVydmFsLgoKRXZlcnkgc3VwcGxpZWQgSW5wdXRGcmFtZSBpcyBjb21wYXJlZCB3aXRoIHRoZSBhY3R1YWwgY29tbWl0dGVkIHJlY29yZC4gVGhlIHJlYWwgaW5wdXQgcHJvZHVjZXIgcmVtYWlucyBlbmFibGVkIHdpdGggYW4gaXNvbGF0ZWQgZW1wdHkgZGV2aWNlIGZpbHRlciBhbmQgbXVzdCBwdWJsaXNoIG5ldXRyYWwgcGh5c2ljYWwgZnJhbWVzIGJlZm9yZSB0aGUgc3VwcGxpZWQgZnJhbWU7IGZvY3VzL2NhcHR1cmUgZmFpbHVyZXMgcmVtYWluIHZpc2libGUuIFRoaXMgZXhlcmNpc2VzIHN1cHBsaWVkIElucHV0RnJhbWVzIGFuZCBhY3R1YWwgSW5wdXQvUnVuL1BsYXllciB3aXJpbmcsIG5vdCBrZXlib2FyZC9nYW1lcGFkIGhhcmR3YXJlLgoKIyMgRnJvemVuIGFjY2VwdGFuY2UgYW5kIGRlbm9taW5hdG9yCgpDaGFpbiBhY2NlcHRhbmNlIHJlcXVpcmVzIGV4YWN0bHkgb25lIHN1Y2Nlc3NmdWwgU2xpZGUsIG9uZSBzbGlkZS1qdW1wIEp1bXAsIG9uZSBzdWNjZXNzZnVsIFZhdWx0LCBhbmQgb25lIHN1Y2Nlc3NmdWwgUmVib3VuZCBpbiB0aGF0IG9yZGVyLCBmb2xsb3dlZCBieSBhbiBhY3R1YWwgc3VjY2Vzc2Z1bCBMYW5kIGFmdGVyIHJlYm91bmQuIEF0IHRoZSBmaXJzdCBmaW5pc2ggY3Jvc3NpbmcgdGhlIHN0YXRlIG11c3QgYmUgR3JvdW5kLCB0aGUgYWN0dWFsIHJlc29sdmVkIHBvc2UgbXVzdCBiZSBncm91bmRlZCwgYW5kIHRoZSB3ZXN0d2FyZCBjb21wb25lbnQgb2YgaG9yaXpvbnRhbCBkZWNpc2lvbiB2ZWxvY2l0eSBtdXN0IGJlIGF0IGxlYXN0IGBTcHJpbnRTcGVlZCAtIDAuMDAwMDEgbS9zYC4gVGhpcyB0b2xlcmFuY2UgY292ZXJzIGZsb2F0aW5nLXBvaW50IHJlcHJlc2VudGF0aW9uIG9mIHRoZSB1bmNoYW5nZWQgZGVmYXVsdCBzcHJpbnQgc3BlZWQ7IGl0IGFkZHMgbm8gbW92ZW1lbnQgdHVuaW5nLiBBbGwgZmFpbGVkIHRyYXZlcnNhbCBmYWN0cywgZXh0cmEganVtcC9zbGlkZS92YXVsdC9yZWJvdW5kIGV2ZW50cyBhbmQgb3JkZXIgZGlzYWdyZWVtZW50cyBhcmUgcmV0YWluZWQgYXMgY2hhaW4gZmFpbHVyZXM7IG5vIHJvdXRlIGV4dGVuc2lvbiByZXBhaXJzIHRoZW0uCgpUaGUgb3JpZ2luYWwgbnVtZXJpY2FsIHRhcmdldHMgcmVtYWluICoqZnJlZSBob3Jpem9udGFsIHNwZWVkIG5lYXJlc3QtcmFuayBwOTAg4omlOSBtL3MqKiBhbmQgKipubyB2ZXJiLXRyYW5zaXRpb24gaW5wdXQtbG9jayBpbnRlcnZhbCA+MC4zNSBzKiouIFRoZSBwb29sZWQgcDkwIGlzIHRoZSBzcGVlZCBhY2NlcHRhbmNlIHN0YXRpc3RpYzsgcGVyLXJ1biBwOTAgdmFsdWVzIGFuZCB0aHJlc2hvbGQgZmxhZ3MgYXJlIHNlcGFyYXRlbHkgZGVzY3JpcHRpdmUuIFRocmVlLXJ1biBjaGFpbiBjb25zaXN0ZW5jeSByZXF1aXJlcyBhbGwgdGhyZWUgY2hhaW4gdmVyZGljdHMgdG8gcGFzcywgYW5kIGxvY2sgYWNjZXB0YW5jZSByZXF1aXJlcyBhbGwgdGhyZWUgcnVucyB0byBoYXZlIG5vIG92ZXItbGltaXQgb3IgY2Vuc29yZWQgcG9zaXRpdmUgbG9jay4gQ29tYmluZWQgYWNjZXB0YW5jZSByZXF1aXJlcyBjb21wbGV0ZSBjb2xsZWN0aW9uIGludGVncml0eSwgYWxsIHRocmVlIGNoYWluIHZlcmRpY3RzLCBwb29sZWQgcDkwIGFjY2VwdGFuY2UsIGFuZCBhbGwgdGhyZWUgbG9jayB2ZXJkaWN0cy4gVXNlIHRoZSBleGlzdGluZyBUZWxlbWV0cnlQcmVzZW50ZXIgZWxpZ2liaWxpdHk6IHZhbGlkIHBsYXllci90aWNrLCBmaW5pdGUgbm9ubmVnYXRpdmUgaG9yaXpvbnRhbCBzcGVlZCwgbm8gY2hhc2UsIG9uZSBzcGVlZCBzYW1wbGUgcGVyIHBsYXllci90aWNrLiBBbGwgcXVhbGlmeWluZyB0aWNrcyByZW1haW4gaW4gZWFjaCBydW4gYW5kIHRoZSBwb29sZWQgZGVub21pbmF0b3IsIGluY2x1ZGluZyBzdGFydHVwIGFuZCBzbG93L2Jsb2NrZWQgdGlja3MuIFBvb2xlZCB2YWx1ZXMgdXNlIGFsbCByZXRhaW5lZCByYXcgZWxpZ2libGUgc3BlZWRzLCBub3QgYW4gYXZlcmFnZSBvZiBwZXItcnVuIHBlcmNlbnRpbGVzLiBJbnZhbGlkL3VuYXZhaWxhYmxlIHNhbXBsZXMgYXJlIHJlcG9ydGVkIGFuZCBuZXZlciBzaWxlbnRseSBkaXNjYXJkZWQgdG8gcmVzY3VlIGFjY2VwdGFuY2UuCgpJbnB1dCBsb2NrcyByZXRhaW4gc3RhcnQsIHJlbGVhc2UsIGR1cmF0aW9uLCByZWFzb24sIGFuZCBhbnkgY2FwdHVyZS1lZGdlIGNlbnNvci4gQW4gdW5maW5pc2hlZCBwb3NpdGl2ZSBsb2NrIGF0IHRoZSB0ZXJtaW5hbCB0aWNrIGlzIGNlbnNvcmVkIGFuZCBjYW5ub3QgcGFzcyBjb21wbGV0ZSBsb2NrIGFjY2VwdGFuY2UuIEZsb2F0IGRlbHRhLXRpbWUgcm91bmRpbmcgdXNlcyBhIHN0YXRlZCBgMWUtNiBzYCBjb21wYXJpc29uIHRvbGVyYW5jZSwgYXMgaW4gdGhlIHByaW9yIHRyYW5zaXRpb24gYWNjZXB0YW5jZTsgaXQgZG9lcyBub3QgcGVybWl0IGFuIGV4dHJhIGxvY2tlZCB0aWNrLgoKUHJlc2VydmUgdGhlIG9yaWdpbmFsIG5hdGl2ZSBpbnB1dC9wcm9iZS9yZXNvbHV0aW9uIGJpbmFyeSwgcmVhZGFibGUgZnVsbCB0aWNrIHJlY29yZHMsIGV2ZXJ5IHRyYW5zaXRpb24gZmFjdCwgbWV0YWRhdGEvc291cmNlL2NvbmZpZy9jb3Vyc2UvZml4dHVyZSBoYXNoZXMsIGFuZCB0ZXJtaW5hdGlvbiByZWFzb24uIERlY29kZSB0aGUgZXhhY3Qgc2F2ZWQgYmluYXJ5IGFuZCB1c2UgdGhlIGV4aXN0aW5nIFBsYXllckNvbnRyb2xsZXIuUmVwbGF5IHBhdGggdG8gY29tcGFyZSBldmVyeSByZWNvcmRlZC1yZXNvbHV0aW9uIHN0YXRlL3ZlbG9jaXR5L2hlYWRpbmcvbG9jay9mYWN0IHRyYWplY3RvcnkuIFJlcGxheSB2ZXJpZmllcyBkZWNpc2lvbnMgZ2l2ZW4gY2FwdHVyZWQgY29sbGlzaW9uIHJlc29sdXRpb25zOyBpdCBkb2VzIG5vdCBjbGFpbSBkZXRlcm1pbmlzdGljIHBoeXNpY3MgZnJvbSBpbnB1dCBhbG9uZS4KIyMgUmV2aXNpb24gMDIzIGFtZW5kbWVudCwgYmVmb3JlIGNvbGxlY3Rpb24KClJldmlzaW9uIDAyMiBpcyBwcmVzZXJ2ZWQgYXQgYExvZ3MvQWdlbnRWYWxpZGF0aW9uL1BMQU4tMDAzL2NvbnRpbnVvdXMtY2hhaW4tMDIyLzIwMjYwOTE1VDE2NTkyODEzOVotNTE2MTRhYTVlZGFmNDNkY2I4Mjk5YmE5YjY1ZWFjNWQvYDsgaXRzIG5hdGl2ZSBOVW5pdCByZXN1bHQgaXMgYHJlc3VsdC1kNmFhMjAyYTc0ZDg0OTBiYmNjMTA5NDhkYWIzN2Y1NGAuIFRoZSBmaXJzdCBhdHRlbXB0IHJldGFpbmVkIDEsODAwIHRpY2tzIGJ1dCBubyB2ZXJiczogYWN0dWFsIGZ1bGwgZWFzdCBpbnB1dCByZWFjaGVkIGRlY2lzaW9uIHNwZWVkIDcuOTk5OTk5IG0vcywgYmVsb3cgdGhlIGV4YWN0IDggbS9zIGFkbWlzc2lvbiBjb21wYXJpc29uLCBzbyBubyBjcm91Y2ggcHJlc3Mgb2NjdXJyZWQuIEl0IHJlYWNoZWQgdGhlIHZhdWx0IGJsb2NrIGFuZCByZW1haW5lZCBob3Jpem9udGFsbHkgc3RhdGlvbmFyeSBkdXJpbmcgdGlja3MgNDfigJMxODAwLiBJdHMgcmV0YWluZWQgcDkwIGlzIHplcm8uIFRoZSBhdHRlbXB0IHJlcGxheWVkIGFsbCAxLDgwMCBjYXB0dXJlZCByZXNvbHV0aW9ucywgdGhlbiBqb3VybmFsIGhhc2hpbmcgdGhyZXcgYSBXaW5kb3dzIHNoYXJpbmcgdmlvbGF0aW9uIHdoaWxlIGl0cyB3cml0ZXIgd2FzIHN0aWxsIG9wZW4uIFRoZSBvdGhlciB0d28gYXR0ZW1wdHMgc3RheWVkIHJlc2VydmVkIGFuZCB1bnN0YXJ0ZWQ7IHJldmlzaW9uIDAyMiBpcyBub3QgYSBjb21wbGV0ZWQgY29ob3J0LgoKUmV2aXNpb24gMDIzIGNoYW5nZXMgb25seSB0aGUgbnVtZXJpY2FsIGludGVycHJldGF0aW9uIG9mIHJlYWNoaW5nIGRlZmF1bHQgc3ByaW50IHNwZWVkIGZvciB0aGUgc29sZSBjcm91Y2ggYWRtaXNzaW9uOiBzdWJ0cmFjdCBhIGZpeGVkIDAuMDAwMDEgbS9zIGZsb2F0IHRvbGVyYW5jZSwgYWxyZWFkeSB1c2VkIGJ5IHRoZSBmaW5hbCBzcHJpbnQgcHJlZGljYXRlLiBUaGlzIGlzIG5vdCBtb3ZlbWVudCB0dW5pbmcgb3IgYSBuZXcgYWNjZXB0YW5jZSBzcGVlZC4gVGhlIGJvZHktcmVsYXRpdmUgaW5wdXQgY2FsY3VsYXRpb24sIGdlb21ldHJ5IGFuZCBtYXJrZXIgcG9zaXRpb25zLCBkZWZhdWx0IHByb2ZpbGUvY29uZmlnLCBzZXF1ZW5jZSwgc2l4LXNsaWRlLXRpY2sgcnVsZSwgdmF1bHQvcmVib3VuZCBhZG1pc3Npb25zLCBwb29sZWQgcmVzb2x2ZWQtc3BlZWQgZGVub21pbmF0b3IsIG9yaWdpbmFsIDkgbS9zIGFuZCAwLjM1IHMgdGFyZ2V0cywgZmlyc3QtZmluaXNoLzEsODAwLXRpY2sgc3RvcHBpbmcgcnVsZXMgYW5kIGFsbC10aHJlZS1hdHRlbXB0IHBvbGljeSByZW1haW4gdW5jaGFuZ2VkLiBUaGVzZSBmcmVzaCBydW5zIGZvcm0gYSBzZXBhcmF0ZSBjb2hvcnQ7IHJldmlzaW9uIDAyMiBkYXRhIHJlbWFpbnMgZXZpZGVuY2UsIG5vdCBhIHJldHJ5IHNpbGVudGx5IG9taXR0ZWQgZnJvbSB0aGlzIHJlcG9ydC4KClRoZSB3cml0ZXIgaXMgc2VhbGVkL2Rpc3Bvc2VkIGJlZm9yZSBqb3VybmFsIHJlb3BlbmluZyBmb3IgaGFzaGluZy4gSm91cm5hbC9yZXBvcnQgZmluYWxpemF0aW9uIGZhaWx1cmVzIG11c3Qgc2V0IGNvbGxlY3Rpb25JbnRlZ3JpdHlQYXNzZWQ9ZmFsc2UgYmVmb3JlIGNvbXBsZXRpb24gc3RhdHVzIGlzIHJldGFpbmVkLiBBIGJpbmFyeSBDb21wbGV0ZT10cnVlIGZsYWcgY29udGludWVzIHRvIGRlc2NyaWJlIG9ubHkgdGhlIG1hbnVhbGx5IGNsb3NlZCBkaWFnbm9zdGljIHRpY2sgd2luZG93LCBuZXZlciBuYXR1cmFsIFJ1bkVuZGVkIG9yIGdhbWVwbGF5LXNlc3Npb24gY29tcGxldGlvbjsgdGhlIG5vcm1hbCBpbmNvbXBsZXRlIGxpZmVjeWNsZS1jbG9zZSByZWNvcmRpbmcgcmVtYWlucyBzZXBhcmF0ZS4NCg==";
        private const int Attempts = 3, TickLimit = 1800;
        private const float Dt = 1f / 60f;
        private static readonly Vector3 Origin = new Vector3(2000f, 0f, 0f);
        private static readonly Vector3 Spawn = Origin + new Vector3(0f, 0.05f, 0f);

        [UnityTest, Explicit("Three frozen 023 runs; NUnit checks collection integrity only. Read separate M1 acceptance verdicts.")]
        public IEnumerator ThreeContinuousChainsRetainAllTicksAndSeparateAcceptanceVerdicts()
        {
            yield return new EnterPlayMode();
            yield return Collect();
        }

        [UnityTearDown]
        public IEnumerator RestoreEditor()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        private static IEnumerator Collect()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "AgentValidation", "PLAN-003",
                "continuous-chain-023", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(folder);
            byte[] protocol = Convert.FromBase64String(ProtocolBase64);
            Assert.That(Hash(protocol), Is.EqualTo(ProtocolSha256));
            File.WriteAllBytes(Path.Combine(folder, "PROTOCOL.md"), protocol);
            File.Copy(Fixture, Path.Combine(folder, "fixture-source.cs"));
            var report = new CohortReport { startedUtc = DateTime.UtcNow.ToString("O"), protocolSha256 = ProtocolSha256,
                fixtureSha256 = Hash(File.ReadAllBytes(Fixture)), sourceRevision = BuildHash("Assets/Scripts", "*.cs"),
                configHash = BuildHash("Assets/Resources/ScriptableObjects", "*.asset"), sceneDependencyHash = AssetDatabase.GetAssetDependencyHash(Arena).ToString(),
                runs = Enumerable.Range(1, Attempts).Select(i => new RunReport { attempt = i }).ToArray() };
            Write(Path.Combine(folder, "pre-run-declaration.json"), report);
            var pooled = new List<double>();
            bool previousBackground = Application.runInBackground;
            using (var gate = new CaptureGateTrace("ContinuousChain023"))
            {
                Application.runInBackground = true;
                try
                {
                    yield return gate.AdmitStableGameViewFocus();
                    foreach (RunReport runReport in report.runs)
                    {
                        using (var trial = new Trial(report, runReport, folder))
                        {
                            SceneManager.sceneLoaded += trial.Arrange;
                            try
                            {
                                runReport.status = "loading";
                                Write(Path.Combine(folder, "cohort-report.json"), report);
                                var load = SceneManager.LoadSceneAsync(Arena, LoadSceneMode.Single);
                                if (load == null) trial.Fail("Scene load returned null.");
                                float start = Time.realtimeSinceStartup;
                                while (!trial.Ready && !trial.Failed && Time.realtimeSinceStartup - start < 20f) yield return null;
                                if (!trial.Ready || load == null || !load.isDone) trial.Fail("Fresh scene did not become ready before the 20-second admission deadline.");
                                start = Time.realtimeSinceStartup;
                                while (!trial.Done && !trial.Failed && Time.realtimeSinceStartup - start < 90f) yield return null;
                                if (!trial.Done && !trial.Failed) trial.Fail("90-second wall-clock integrity timeout; partial attempt retained.");
                                trial.Close();
                                trial.Analyze();
                                pooled.AddRange(trial.EligibleSpeeds);
                            }
                            finally { SceneManager.sceneLoaded -= trial.Arrange; }
                        }
                        Write(Path.Combine(folder, "cohort-report.json"), report);
                        if (!runReport.collectionIntegrityPassed) break;
                    }
                    report.pooledFreeSpeedSamples = pooled.Count;
                    report.pooledFreeSpeedP90Available = pooled.Count > 0;
                    report.pooledFreeSpeedP90 = Percentile(pooled);
                    report.collectionIntegrityPassed = report.runs.All(r => r.collectionIntegrityPassed);
                    report.chainAcceptancePassed = report.runs.All(r => r.chainAcceptancePassed);
                    report.speedAcceptancePassed = report.pooledFreeSpeedP90Available && report.pooledFreeSpeedP90 >= 9d;
                    report.lockAcceptancePassed = report.runs.All(r => r.lockAcceptancePassed);
                    report.acceptancePassed = report.collectionIntegrityPassed && report.chainAcceptancePassed && report.speedAcceptancePassed && report.lockAcceptancePassed;
                    report.finishedUtc = DateTime.UtcNow.ToString("O");
                    TestContext.WriteLine("Continuous chain 023: NUnit=collection integrity only; integrity=" + report.collectionIntegrityPassed +
                        "; chain=" + report.chainAcceptancePassed + "; pooled p90=" + report.pooledFreeSpeedP90.ToString("R", CultureInfo.InvariantCulture) +
                        "; speed=" + report.speedAcceptancePassed + "; locks=" + report.lockAcceptancePassed + "; combined M1=" + report.acceptancePassed + "; report=" + folder);
                }
                finally
                {
                    report.gateDiagnostics = gate.Describe();
                    Write(Path.Combine(folder, "cohort-report.json"), report);
                    Application.runInBackground = previousBackground;
                }
            }
            Assert.That(report.collectionIntegrityPassed, Is.True, "Capture integrity failed; retained partial report: " + folder);
        }

        [Serializable] private sealed class CohortReport
        {
            public string protocolSha256, fixtureSha256, sourceRevision, configHash, sceneDependencyHash, startedUtc, finishedUtc, gateDiagnostics;
            public string scope = "Explicit capture-only; arranged empty-room M1; no authored-route, chase, hardware or human acceptance";
            public string captureCompleteness = "Original binary Complete=true marks this manually closed diagnostic tick window only; no natural RunEnded/gameplay-session completion is asserted or manufactured. Run suspension separately retains an incomplete lifecycle-close recording.";
            public string speedDenominator = "All qualifying free horizontal resolved-velocity ticks from PlayerMovementSample; pooled nearest rank; no startup/blocked/slow tick exclusions";
            public string stoppingRule = "First resolved x<=1997 or 1800 committed ticks; all 3 despite chain/numeric misses; integrity failures only stop subsequent attempts";
            public int plannedRuns = Attempts, seed = 1, maximumTicks = TickLimit, pooledFreeSpeedSamples;
            public float fixedDeltaTime = Dt, originalSpeedTarget = 9f, originalLockLimitSeconds = 0.35f;
            public bool collectionIntegrityPassed, chainAcceptancePassed, speedAcceptancePassed, lockAcceptancePassed, acceptancePassed, pooledFreeSpeedP90Available;
            public double pooledFreeSpeedP90;
            public RunReport[] runs;
        }

        [Serializable] private sealed class RunReport
        {
            public int attempt, committedTicks, physicalFrames, movementEvents, traversalEvents, freeSpeedSamples, invalidTelemetrySamples, duplicateTelemetrySamples, outOfOrderTelemetrySamples;
            public string status = "reserved-not-started", failure = "", termination = "", startedUtc, finishedUtc, sessionId, nativePath, nativeSha256, rawRowsSha256;
            public string lifecycleClosePath, lifecycleCloseSha256, courseSha256;
            public string sourceRevision, configHash, profileAsset, profileSha256, moverAsset, moverSha256, timeline = "", chainExplanation = "", fixtureSha256, protocolSha256;
            public string chainPredicate = "One Slide, one Jump from Slide, one Vault, one Rebound in order; no failures/extras; later Land; first finish crossing Ground + resolved pose grounded + westward decision vx>=SprintSpeed-0.00001";
            public bool collectionIntegrityPassed, chainAcceptancePassed, speedThresholdReachedDescriptive, lockAcceptancePassed, replayPassed, freeSpeedP90Available, finishReached;
            public double freeSpeedP90, maximumCompleteLockSeconds;
            public int replayComparedRecords, censoredLocks;
            public LockRow[] locks = Array.Empty<LockRow>();
            public PhaseRow[] phases = Array.Empty<PhaseRow>();
            public StationaryRow[] horizontalStationaryIntervals = Array.Empty<StationaryRow>();
            public string stationarityScope = "Exact zero resolved horizontal velocity intervals are descriptive, including deliberate vertical vault arc phases. No stall threshold is invented; partial/slow motion remains in raw ticks and phase minima for without-a-hitch review.";
        }
        [Serializable] private sealed class LockRow
        {
            public long startTick, releaseTick, observedThroughTick;
            public string reason;
            public double seconds;
            public bool censored;
        }
        [Serializable] private sealed class PhaseRow
        {
            public string phase;
            public long firstTick, lastTick;
            public int ticks, exactlyStationaryHorizontalTicks;
            public double minimumDecisionHorizontalSpeed, minimumResolvedHorizontalSpeed, minimumForwardResolvedSpeed;
        }
        [Serializable] private sealed class StationaryRow
        {
            public long firstTick, lastTick;
            public string phase, movementState;
            public double seconds, maximumDecisionHorizontalSpeed;
        }
        private sealed class Sample
        {
            public InputProbeRecord record;
            public PlayerMovementSample movement;
            public Vector3 decisionVelocity;
            public PlayerTraversalFact[] facts;
            public string phase;
        }

        private sealed class Trial : IDisposable
        {
            private readonly CohortReport cohort;
            private readonly RunReport report;
            private readonly string folder;
            private readonly List<Sample> samples = new List<Sample>();
            private readonly List<PlayerTraversalFact> publishedFacts = new List<PlayerTraversalFact>();
            private readonly TelemetryPresenter telemetry = new TelemetryPresenter();
            private readonly TelemetryDriverState telemetryState = new TelemetryDriverState();
            private readonly StreamWriter rows;
            private RunSessionManager run, captureRun;
            private InputManager input;
            private PlayerManager player;
            private ChaseManager chase;
            private PlayerProfile profile;
            private PlayerMoverDriverConfig mover;
            private InputActionMap gameplay;
            private ReadOnlyArray<InputDevice>? previousDevices;
            private RunCaptureMetadata metadata;
            private GameObject arrangement;
            private string profileSnapshot, moverSnapshot;
            private bool restoreDevices, closed, slideRequested, slideJumpRequested, vaultRequested, reboundRequested, slideJumpObserved, vaultObserved, reboundObserved;
            private int captures, slideTicks;
            private InputButtons previousHeld;
            private InputFrame supplied;
            private string phase = "sprint-approach";
            public bool Ready { get; private set; }
            public bool Done { get; private set; }
            public bool Failed => report.failure.Length != 0;
            public readonly List<double> EligibleSpeeds = new List<double>();

            public Trial(CohortReport cohort, RunReport report, string parent)
            {
                this.cohort = cohort; this.report = report;
                folder = Path.Combine(parent, "attempt-" + report.attempt);
                Directory.CreateDirectory(folder);
                report.startedUtc = DateTime.UtcNow.ToString("O");
                report.protocolSha256 = cohort.protocolSha256; report.fixtureSha256 = cohort.fixtureSha256;
                rows = new StreamWriter(Path.Combine(folder, "ticks.jsonl"), false, new UTF8Encoding(false)) { AutoFlush = true };
            }

            public void Arrange(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != Arena) return;
                try
                {
                    VerifyFiles();
                    var root = One<TagArenaSceneRoot>();
                    profile = (PlayerProfile)new SerializedObject(root).FindProperty("_playerProfile").objectReferenceValue;
                    Assert.That(profile, Is.Not.Null);
                    profileSnapshot = EditorJsonUtility.ToJson(profile);
                    report.profileAsset = AssetDatabase.GetAssetPath(profile); report.profileSha256 = Hash(File.ReadAllBytes(report.profileAsset));
                    arrangement = new GameObject("[Test only] Continuous chain 022; aerial vault endpoint; no shipped content");
                    arrangement.transform.position = Origin;
                    Box("Floor", new Vector3(4f, -0.25f, 0f), new Vector3(32f, 0.5f, 12f));
                    Marker(Box("Vault block", new Vector3(6.375f, 0.6f, 0f), new Vector3(0.75f, 1.2f, 4f)),
                        94001, LevelMarkerKind.VaultSurface, Origin + new Vector3(7.1f, 1.2f, 0f));
                    Marker(Box("Rebound wall", new Vector3(8.30f, 3f, 0f), new Vector3(0.5f, 6f, 6f)),
                        94002, LevelMarkerKind.ReboundSurface, Vector3.zero);
                    Field(root, "_spawnPosition").SetValue(root, Spawn);
                    Field(root, "_seed").SetValue(root, 1);
                    captureRun = RunSessionManager.Instance != null ? RunSessionManager.Instance : (RunSessionManager)Field(root, "_run").GetValue(root);
                    Assert.That(captureRun, Is.Not.Null);
                    captureRun.CaptureStarted += CaptureStarted;
                    TagArenaSceneRoot.SceneReady += SceneReady;
                    Physics.SyncTransforms();
                    string course = Json(new { origin = Origin, initialSpawn = Spawn, initialHeading = 90f, finishX = Origin.x - 3f,
                        objects = arrangement.GetComponentsInChildren<BoxCollider>().Select(box => new { name = box.name,
                            worldCenter = box.bounds.center, worldSize = box.bounds.size, enabled = box.enabled, isTrigger = box.isTrigger,
                            marker = box.GetComponent<LevelMarker>() == null ? null : new { id = box.GetComponent<LevelMarker>().SurfaceId,
                                kind = box.GetComponent<LevelMarker>().MarkerKind, target = box.GetComponent<LevelMarker>().Target } }).ToArray() });
                    File.WriteAllText(Path.Combine(folder, "actual-course.json"), course, new UTF8Encoding(false));
                    report.courseSha256 = Hash(File.ReadAllBytes(Path.Combine(folder, "actual-course.json")));
                }
                catch (Exception error) { Fail("Arrangement: " + error); }
            }

            private BoxCollider Box(string name, Vector3 position, Vector3 size)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "[022] " + name; box.transform.SetParent(arrangement.transform, false);
                box.transform.localPosition = position; box.transform.localScale = size;
                return box.GetComponent<BoxCollider>();
            }

            private static void Marker(BoxCollider box, int id, LevelMarkerKind kind, Vector3 target)
            {
                var fields = new SerializedObject(box.gameObject.AddComponent<LevelMarker>());
                fields.FindProperty("_id").intValue = id; fields.FindProperty("_kind").enumValueIndex = (int)kind;
                fields.FindProperty("_roomId").intValue = 2; fields.FindProperty("_size").vector3Value = box.bounds.size;
                fields.FindProperty("_targetPosition").vector3Value = target; fields.ApplyModifiedPropertiesWithoutUndo();
            }

            private void CaptureStarted(RunCaptureMetadata value)
            {
                if (++captures != 1) { Fail("Repeated capture start; original metadata retained."); return; }
                metadata = value;
                report.sessionId = value.SessionId; report.sourceRevision = value.SourceRevision; report.configHash = value.ConfigSnapshotHash;
                File.WriteAllText(Path.Combine(folder, "native-metadata.json"), Json(value));
                telemetry.Begin(telemetryState, value);
            }

            private void SceneReady(SceneKey scene)
            {
                if (scene != SceneKey.TagArena) return;
                try
                {
                    run = RunSessionManager.Instance; input = InputManager.Instance; player = One<PlayerManager>(); chase = One<ChaseManager>();
                    Assert.That(run.Tick, Is.Zero); Assert.That(PlayerRegistry.Items.Count, Is.EqualTo(1));
                    Assert.That(player.ReadOnlyState.Position, Is.EqualTo(Spawn)); Assert.That(player.ReadOnlyState.HeadingDegrees, Is.EqualTo(90f));
                    Assert.That(player.ReadOnlyState.Velocity, Is.EqualTo(Vector3.zero));
                    Assert.That(captures, Is.EqualTo(1)); Assert.That(metadata.StartTick, Is.Zero); Assert.That(metadata.Seed, Is.EqualTo(1));
                    Assert.That(metadata.FixedDeltaTime, Is.EqualTo(Dt)); Assert.That(Time.fixedDeltaTime, Is.EqualTo(Dt));
                    Assert.That(metadata.SourceRevision, Is.EqualTo(cohort.sourceRevision)); Assert.That(metadata.ConfigSnapshotHash, Is.EqualTo(cohort.configHash));
                    foreach (var hunter in UnityEngine.Object.FindObjectsByType<HunterManager>(FindObjectsSortMode.None)) hunter.gameObject.SetActive(false);
                    Assert.That(HunterRegistry.Items.Count, Is.Zero); Assert.That(chase.ReadOnlyState.HasActiveChase, Is.False);
                    mover = (PlayerMoverDriverConfig)new SerializedObject(player.GetComponent<PlayerDriver>()).FindProperty("_config").objectReferenceValue;
                    Assert.That(mover, Is.Not.Null); moverSnapshot = EditorJsonUtility.ToJson(mover);
                    report.moverAsset = AssetDatabase.GetAssetPath(mover); report.moverSha256 = Hash(File.ReadAllBytes(report.moverAsset));
                    Assert.That(profile.SprintSpeed, Is.EqualTo(8f)); Assert.That(profile.MaxDesignSpeed, Is.EqualTo(14f));
                    Assert.That(profile.VaultDuration, Is.EqualTo(0.25f)); Assert.That(profile.SlideBoost, Is.EqualTo(2f));
                    File.WriteAllText(Path.Combine(folder, "profile.json"), profileSnapshot); File.WriteAllText(Path.Combine(folder, "mover.json"), moverSnapshot);
                    gameplay = (InputActionMap)Field(input.GetComponent<PlayerInputDriver>(), "_actions").GetValue(input.GetComponent<PlayerInputDriver>());
                    Assert.That(gameplay, Is.Not.Null);
                    previousDevices = gameplay.devices.HasValue ? new ReadOnlyArray<InputDevice>(gameplay.devices.Value.ToArray()) : (ReadOnlyArray<InputDevice>?)null;
                    restoreDevices = true; gameplay.devices = Array.Empty<InputDevice>();
                    foreach (InputAction action in gameplay.actions) Assert.That(action.controls.Count, Is.Zero);
                    Assert.That(input.SetSource(InputSource.Live), Is.True);
                    input.FramePublished += Supply;
                    run.PlayerProbeRecorded += Record; run.PlayerMovementPublished += Movement; run.PlayerTraversalPublished += Traversal; run.TickAdvanced += Committed;
                    Ready = true; report.status = "collecting";
                    Write(Path.Combine(folder, "run-report.json"), report);
                }
                catch (Exception error) { Fail("Scene readiness: " + error); }
            }

            private void Supply(InputFrame physical)
            {
                if (closed || Done || Failed) { run.ReceiveInput(default); return; }
                try
                {
                    Assert.That(physical, Is.EqualTo(default(InputFrame)), "Isolated hardware producer must remain neutral.");
                    report.physicalFrames++;
                    var state = player.ReadOnlyState; var probe = player.LastProbeRecord.Probe;
                    InputButtons held = InputButtons.Sprint;
                    if (!slideRequested && state.Position.x >= Origin.x + 1f && Speed(state.Velocity) >= profile.SprintSpeed - 0.00001f)
                        slideRequested = true;
                    if (slideRequested && !slideJumpRequested) held |= InputButtons.Crouch;
                    if (slideRequested && !slideJumpRequested && slideTicks >= 6)
                    { slideJumpRequested = true; held = InputButtons.Sprint | InputButtons.Jump; }
                    else if (slideJumpObserved && !vaultRequested && (previousHeld & InputButtons.Jump) == 0 &&
                        probe.VaultCandidate && probe.VaultHeight >= profile.VaultMinimumHeight && probe.VaultHeight <= profile.VaultMaximumHeight &&
                        probe.VaultClearance > 0f && !probe.StandingBlocked)
                    { vaultRequested = true; held |= InputButtons.Jump; }
                    else if (vaultObserved && !reboundRequested && (previousHeld & InputButtons.Jump) == 0 && state.MovementState == MovementState.Air &&
                        probe.WallDetected && probe.WallId == 94002 && probe.WallDistance <= profile.ReboundDistance && probe.WallAngleDegrees <= profile.ReboundAngle)
                    { reboundRequested = true; held |= InputButtons.Jump; }
                    float yaw = reboundObserved ? Mathf.Clamp(Mathf.DeltaAngle(state.HeadingDegrees, 270f), -15f, 15f) : 0f;
                    float nextHeading = state.HeadingDegrees + yaw;
                    Vector3 forward = Quaternion.Euler(0f, nextHeading, 0f) * Vector3.forward;
                    Vector3 right = Vector3.Cross(Vector3.up, forward), desired = reboundObserved ? Vector3.left : Vector3.right;
                    var move = new Vector2(Vector3.Dot(desired, right), Vector3.Dot(desired, forward));
                    supplied = new InputFrame(move, new Vector2(yaw, 0f), held, held & ~previousHeld, previousHeld & ~held);
                    previousHeld = held;
                    phase = reboundObserved ? "west-return-after-rebound" : vaultObserved ? "rebound-approach" : vaultRequested ? "vault-request-through-completion" :
                        slideJumpObserved ? "air-vault-approach" : slideJumpRequested ? "slide-jump-request" : slideRequested ? "slide" : "sprint-approach";
                    run.ReceiveInput(supplied);
                }
                catch (Exception error) { Fail("Input: " + error); run.ReceiveInput(default); }
            }

            private void Record(InputProbeRecord record)
            {
                if (closed) return;
                var sample = new Sample { record = record, movement = player.LastMovementSample, decisionVelocity = player.ReadOnlyState.Velocity,
                    facts = player.LastTraversalFacts.ToArray(), phase = phase };
                samples.Add(sample); report.committedTicks = samples.Count;
                try
                {
                    rows.WriteLine("{\"record\":" + Json(record) + ",\"suppliedInput\":" + Json(supplied) + ",\"movement\":" + Json(sample.movement) +
                        ",\"decisionVelocity\":" + Json(sample.decisionVelocity) + ",\"facts\":" + Json(sample.facts) + ",\"phase\":" + Json(phase) + "}");
                    Assert.That(record.Tick, Is.EqualTo(samples.Count)); Assert.That(record.Input, Is.EqualTo(supplied));
                    Assert.That(report.physicalFrames, Is.EqualTo(samples.Count)); Assert.That(record.DeltaTime, Is.EqualTo(Dt));
                    Assert.That(record.Resolution.Present, Is.True); Assert.That(record.SchemaVersion, Is.EqualTo(InputProbeRecord.CurrentSchemaVersion));
                    Assert.That(record.Input.Move.sqrMagnitude, Is.GreaterThan(0.99f)); Assert.That(record.Input.Held & InputButtons.Sprint, Is.EqualTo(InputButtons.Sprint));
                    Assert.That(Finite(record.Resolution.Position.sqrMagnitude) && Finite(record.Resolution.Velocity.sqrMagnitude) && Finite(sample.decisionVelocity.sqrMagnitude), Is.True);
                    Assert.That(Finite(sample.movement.InputLockSeconds) && sample.movement.InputLockSeconds >= 0f, Is.True);
                    Assert.That(sample.movement.Id, Is.EqualTo(player.Id)); Assert.That(sample.movement.Tick, Is.EqualTo(record.Tick));
                    Assert.That(player.ReadOnlyState.IsAlive && !chase.ReadOnlyState.HasActiveChase && HunterRegistry.Items.Count == 0, Is.True);
                    foreach (var row in telemetry.ConvertMovement(telemetryState, sample.movement)) telemetry.Record(telemetryState, row);
                    foreach (var fact in sample.facts)
                    {
                        foreach (var row in telemetry.ConvertTraversal(telemetryState, fact)) telemetry.Record(telemetryState, row);
                        if (fact.Kind == TraversalKind.Jump && fact.Succeeded && samples.Count > 1 && samples[samples.Count - 2].movement.MovementState == MovementState.Slide)
                            slideJumpObserved = true;
                        if (fact.Kind == TraversalKind.Vault && fact.Succeeded) vaultObserved = true;
                        if (fact.Kind == TraversalKind.Rebound && fact.Succeeded) reboundObserved = true;
                    }
                    if (sample.movement.MovementState == MovementState.Slide) slideTicks++;
                    if (record.Resolution.Position.x <= Origin.x - 3f) { report.finishReached = true; report.termination = "first-return-finish-crossing"; Done = true; }
                    else if (samples.Count >= TickLimit) { report.termination = "1800-committed-tick-limit"; Done = true; }
                }
                catch (Exception error) { Fail("Committed record: " + error); }
            }

            private void Movement(PlayerMovementSample value)
            {
                if (closed) return;
                try { report.movementEvents++; Assert.That(value, Is.EqualTo(samples.Last().movement)); }
                catch (Exception error) { Fail("Movement publication: " + error); }
            }
            private void Traversal(PlayerTraversalFact value) { if (!closed) { publishedFacts.Add(value); report.traversalEvents++; } }
            private void Committed(InputFrame frame, float dt, long tick)
            {
                if (closed) return;
                try
                {
                    Assert.That(tick, Is.EqualTo(samples.Count)); Assert.That(frame, Is.EqualTo(supplied)); Assert.That(dt, Is.EqualTo(Dt));
                    Assert.That(report.movementEvents, Is.EqualTo(samples.Count));
                    Assert.That(publishedFacts, Is.EqualTo(samples.SelectMany(s => s.facts).ToArray()));
                }
                catch (Exception error) { Fail("Tick publication: " + error); }
                if (Done || Failed) Close();
            }

            public void Fail(string message)
            {
                if (!Failed) report.failure = message;
                report.collectionIntegrityPassed = false;
                if (!Done) report.termination = "integrity-failure";
            }

            public void Close()
            {
                if (closed) return;
                closed = true;
                try
                {
                    if (input != null && captures == 1)
                    {
                        Assert.That(input.SaveRecording(samples.Count, Done && !Failed), Is.True, input.LastRecordingError);
                        report.nativePath = input.LastRecordingPath;
                        byte[] bytes = File.ReadAllBytes(report.nativePath); report.nativeSha256 = Hash(bytes);
                        File.WriteAllBytes(Path.Combine(folder, "original.winput"), bytes);
                    }
                }
                catch (Exception error) { Fail("Native save: " + error); }
                finally
                {
                    if (run != null) run.SuspendForSceneLoad();
                    // Normal Run suspension closes the session as incomplete and
                    // writes another native file. Preserve that lifecycle artifact
                    // separately; replay uses the exact bounded-window original.
                    if (input != null && !string.IsNullOrEmpty(input.LastRecordingPath) && input.LastRecordingPath != report.nativePath)
                    {
                        report.lifecycleClosePath = input.LastRecordingPath;
                        byte[] bytes = File.ReadAllBytes(report.lifecycleClosePath); report.lifecycleCloseSha256 = Hash(bytes);
                        File.WriteAllBytes(Path.Combine(folder, "lifecycle-close.winput"), bytes);
                    }
                    rows.Flush();
                }
            }

            public void Analyze()
            {
                bool analysisPassed = false;
                try
                {
                    var metric = telemetry.Finish(telemetryState, samples.Count, Done && !Failed);
                    var csv = new TelemetryCsvPresenter();
                    File.WriteAllLines(Path.Combine(folder, "telemetry.csv"), new[] { csv.Header }.Concat(csv.Metadata(metadata))
                        .Concat(telemetryState.Samples.Select(csv.Raw)).Concat(csv.Summary(metric, samples.Count)));
                    report.invalidTelemetrySamples = metric.InvalidSamples; report.duplicateTelemetrySamples = metric.DuplicateSamples;
                    report.outOfOrderTelemetrySamples = metric.OutOfOrderSamples; report.freeSpeedSamples = metric.FreeSpeedSamples;
                    report.freeSpeedP90Available = metric.FreeSpeedP90.HasValue; report.freeSpeedP90 = metric.FreeSpeedP90 ?? 0d;
                    report.speedThresholdReachedDescriptive = metric.FreeSpeedP90.HasValue && metric.FreeSpeedP90.Value >= 9d;
                    EligibleSpeeds.AddRange(telemetryState.Samples.Where(s => s.Kind == TelemetrySampleKind.HorizontalSpeed && s.Player.IsValid &&
                        s.Tick >= metadata.StartTick && s.Tick <= samples.Count && !s.InChase && Finite(s.Value) && s.Value >= 0f)
                        .GroupBy(s => new { s.Player, s.Tick }).Select(g => (double)g.First().Value));
                    AnalyzeChainAndLocks();
                    VerifyFiles();
                    Assert.That(EditorJsonUtility.ToJson(profile), Is.EqualTo(profileSnapshot)); Assert.That(EditorJsonUtility.ToJson(mover), Is.EqualTo(moverSnapshot));
                    Assert.That(metric.InvalidSamples + metric.DuplicateSamples + metric.OutOfOrderSamples, Is.Zero);
                    Assert.That(metric.FreeSpeedSamples, Is.EqualTo(samples.Count)); Assert.That(EligibleSpeeds.Count, Is.EqualTo(metric.FreeSpeedSamples));
                    Assert.That(Percentile(EligibleSpeeds), Is.EqualTo(report.freeSpeedP90));
                    Assert.That(metric.CompletedLocks, Is.EqualTo(report.locks.Count(l => !l.censored)));
                    Assert.That(metric.IncompleteLocks, Is.EqualTo(report.censoredLocks));
                    Assert.That(metric.MaximumInputLockSeconds ?? 0d, Is.EqualTo(report.maximumCompleteLockSeconds).Within(0.000001d));
                    Replay();
                    analysisPassed = Done && !Failed && report.replayPassed;
                }
                catch (Exception error) { Fail("Post-capture integrity: " + error); }
                finally
                {
                    // Windows readers reject the still-open writer's share mode.
                    // Seal this already-closed tick interval before hashing it;
                    // any finalization error is a collection integrity failure.
                    try
                    {
                        rows.Dispose();
                        report.rawRowsSha256 = Hash(File.ReadAllBytes(Path.Combine(folder, "ticks.jsonl")));
                    }
                    catch (Exception error) { Fail("Tick journal finalization: " + error); }
                    report.collectionIntegrityPassed = analysisPassed && !Failed;
                    report.finishedUtc = DateTime.UtcNow.ToString("O"); report.status = report.collectionIntegrityPassed ? "complete-window" : "partial-or-integrity-failed";
                    try { Write(Path.Combine(folder, "run-report.json"), report); }
                    catch (Exception error)
                    {
                        Fail("Run report finalization: " + error);
                        report.status = "partial-or-integrity-failed";
                    }
                }
            }

            private void AnalyzeChainAndLocks()
            {
                var facts = samples.SelectMany(s => s.facts).ToArray();
                report.timeline = string.Join("; ", facts.Select(f => f.Tick + ":" + f.Kind + ":" + f.Succeeded));
                var verbs = facts.Where(f => f.Kind != TraversalKind.Land).ToArray();
                bool ordered = verbs.Select(f => f.Kind).SequenceEqual(new[] { TraversalKind.Slide, TraversalKind.Jump, TraversalKind.Vault, TraversalKind.Rebound }) &&
                    facts.All(f => f.Succeeded) && verbs.Zip(verbs.Skip(1), (a, b) => a.Tick < b.Tick).All(x => x);
                var last = samples.LastOrDefault();
                bool landed = ordered && facts.Any(f => f.Kind == TraversalKind.Land && f.Succeeded && f.Tick > verbs[3].Tick);
                bool sprint = last != null && last.movement.MovementState == MovementState.Ground && last.record.Resolution.Grounded && -last.decisionVelocity.x >= profile.SprintSpeed - 0.00001f;
                report.chainAcceptancePassed = report.finishReached && ordered && slideJumpObserved && landed && sprint;
                report.chainExplanation = "finish=" + report.finishReached + "; exact ordered successful verbs=" + ordered + "; jump from Slide=" + slideJumpObserved +
                    "; post-rebound Land=" + landed + "; grounded west sprint at first crossing=" + sprint;
                var locks = new List<LockRow>(); LockRow active = null;
                foreach (Sample sample in samples)
                {
                    bool locked = sample.movement.InputLockSeconds > 0f;
                    if (locked && active == null) active = new LockRow { startTick = sample.record.Tick, reason = sample.movement.MovementState.ToString() };
                    if (active != null) active.observedThroughTick = sample.record.Tick;
                    if (!locked && active != null)
                    { active.releaseTick = sample.record.Tick; active.seconds = (active.releaseTick - active.startTick) * (double)Dt; locks.Add(active); active = null; }
                }
                if (active != null) { active.censored = true; active.seconds = (active.observedThroughTick - active.startTick + 1) * (double)Dt; locks.Add(active); }
                report.locks = locks.ToArray(); report.censoredLocks = locks.Count(l => l.censored);
                report.maximumCompleteLockSeconds = locks.Where(l => !l.censored).Select(l => l.seconds).DefaultIfEmpty(0d).Max();
                report.lockAcceptancePassed = Done && locks.All(l => !l.censored && l.seconds <= 0.35d + 0.000001d);
                report.phases = samples.GroupBy(s => s.phase).Select(g => new PhaseRow { phase = g.Key, firstTick = g.First().record.Tick,
                    lastTick = g.Last().record.Tick, ticks = g.Count(), minimumDecisionHorizontalSpeed = g.Min(s => Speed(s.decisionVelocity)),
                    minimumResolvedHorizontalSpeed = g.Min(s => Speed(s.record.Resolution.Velocity)),
                    minimumForwardResolvedSpeed = g.Min(s => s.record.Resolution.Velocity.x * (g.Key == "west-return-after-rebound" ? -1d : 1d)),
                    exactlyStationaryHorizontalTicks = g.Count(s => s.record.Resolution.Velocity.x == 0f && s.record.Resolution.Velocity.z == 0f) }).ToArray();
                var stationary = new List<StationaryRow>(); StationaryRow interval = null;
                foreach (Sample sample in samples)
                {
                    bool stopped = sample.record.Resolution.Velocity.x == 0f && sample.record.Resolution.Velocity.z == 0f;
                    if (!stopped) { interval = null; continue; }
                    string movementState = sample.movement.MovementState.ToString();
                    if (interval == null || interval.phase != sample.phase || interval.movementState != movementState)
                    {
                        interval = new StationaryRow { firstTick = sample.record.Tick, phase = sample.phase, movementState = movementState };
                        stationary.Add(interval);
                    }
                    interval.lastTick = sample.record.Tick; interval.seconds = (interval.lastTick - interval.firstTick + 1) * (double)Dt;
                    interval.maximumDecisionHorizontalSpeed = Math.Max(interval.maximumDecisionHorizontalSpeed, Speed(sample.decisionVelocity));
                }
                report.horizontalStationaryIntervals = stationary.ToArray();
            }

            private void Replay()
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(folder, "original.winput"));
                Assert.That(new InputRecordingPresenter().TryDecode(bytes, out var decodedMetadata, out var decoded, out string error), Is.True, error);
                Assert.That(decodedMetadata, Is.EqualTo(metadata)); Assert.That(decoded.Length, Is.EqualTo(samples.Count));
                var state = new PlayerBehaviorState(); var controller = new PlayerController(state, profile, new System.Random(1));
                controller.Reset(player.Id, Spawn, 90f);
                for (int i = 0; i < decoded.Length; i++)
                {
                    Sample expected = samples[i]; Assert.That(decoded[i], Is.EqualTo(expected.record), "Native record " + i);
                    controller.Replay(decoded[i]);
                    Assert.That(state.Position, Is.EqualTo(expected.movement.Position), "Replay position " + i);
                    Assert.That(state.Velocity, Is.EqualTo(expected.decisionVelocity), "Replay decision velocity " + i);
                    Assert.That(state.LastMovementSample, Is.EqualTo(expected.movement), "Replay state/heading/lock/eye " + i);
                    Assert.That(state.LastTraversalFacts.ToArray(), Is.EqualTo(expected.facts), "Replay facts " + i);
                    report.replayComparedRecords++;
                }
                report.replayPassed = true;
            }

            private void VerifyFiles()
            {
                Assert.That(BuildHash("Assets/Scripts", "*.cs"), Is.EqualTo(cohort.sourceRevision));
                Assert.That(BuildHash("Assets/Resources/ScriptableObjects", "*.asset"), Is.EqualTo(cohort.configHash));
                Assert.That(Hash(File.ReadAllBytes(Fixture)), Is.EqualTo(cohort.fixtureSha256));
                Assert.That(AssetDatabase.GetAssetDependencyHash(Arena).ToString(), Is.EqualTo(cohort.sceneDependencyHash));
            }

            public void Dispose()
            {
                Close();
                TagArenaSceneRoot.SceneReady -= SceneReady;
                if (captureRun != null) captureRun.CaptureStarted -= CaptureStarted;
                if (input != null) input.FramePublished -= Supply;
                if (run != null) { run.PlayerProbeRecorded -= Record; run.PlayerMovementPublished -= Movement; run.PlayerTraversalPublished -= Traversal; run.TickAdvanced -= Committed; }
                if (restoreDevices && gameplay != null) gameplay.devices = previousDevices;
                rows.Dispose();
                if (arrangement != null) UnityEngine.Object.Destroy(arrangement);
            }
        }

        private static T One<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None).Single();
        private static FieldInfo Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(owner.GetType().Name, name);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static double Speed(Vector3 velocity) => (float)Math.Sqrt((double)velocity.x * velocity.x + (double)velocity.z * velocity.z);
        private static double Percentile(List<double> values) => values.Count == 0 ? 0d : values.OrderBy(v => v).ElementAt((int)Math.Ceiling(values.Count * 0.9d) - 1);
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        private static string BuildHash(string directory, string pattern)
        {
            var text = new StringBuilder();
            foreach (string path in Directory.GetFiles(directory, pattern, SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
                text.Append(path.Replace('\\', '/')).Append(Environment.NewLine).Append(File.ReadAllText(path)).Append(Environment.NewLine);
            return "sha256:" + Hash(Encoding.UTF8.GetBytes(text.ToString()));
        }
        private static void Write(string path, object value) => File.WriteAllText(path, JsonUtility.ToJson(value, true), new UTF8Encoding(false));

        // Core records expose immutable properties, which JsonUtility omits. This
        // diagnostic serializer emits every public DTO property with invariant
        // values; the authoritative binary is retained independently and decoded.
        private static string Json(object value)
        {
            if (value == null) return "null";
            if (value is string text) return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
            if (value is bool flag) return flag ? "true" : "false";
            if (value is Enum) return Json(value.ToString());
            if (value is Vector2 v2) return "{\"x\":" + Json(v2.x) + ",\"y\":" + Json(v2.y) + "}";
            if (value is Vector3 v3) return "{\"x\":" + Json(v3.x) + ",\"y\":" + Json(v3.y) + ",\"z\":" + Json(v3.z) + "}";
            if (value is float f) return Finite(f) ? f.ToString("R", CultureInfo.InvariantCulture) : Json(f.ToString(CultureInfo.InvariantCulture));
            if (value.GetType().IsPrimitive) return Convert.ToString(value, CultureInfo.InvariantCulture);
            if (value is IEnumerable items) return "[" + string.Join(",", items.Cast<object>().Select(Json)) + "]";
            return "{" + string.Join(",", value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0).Select(p => Json(p.Name) + ":" + Json(p.GetValue(value)))) + "}";
        }
    }
}
