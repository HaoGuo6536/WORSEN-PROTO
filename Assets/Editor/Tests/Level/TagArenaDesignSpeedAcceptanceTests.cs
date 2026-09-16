// ============================================================================
// TagArenaDesignSpeedAcceptanceTests.cs
// ============================================================================
// PURPOSE:
//   Collects finite continuous movement witnesses on the authored TagArena,
//   retaining actual Player collisions and the selected Player camera output.
//   An intact diagnostic collection is separate from physical and visual
//   acceptance, so a blocked route remains visible even if NUnit is green.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Level physical integration.
// KEY RESPONSIBILITIES:
//   - Execute the frozen authored route census with full ordinary input.
//   - Preserve every tick, probe, result, slow segment and original camera image.
//   - Verify incomplete native bytes and recorded-resolution source replay.
//   - Seal journals before accepting integrity and retain finalization failures.
//   - Bind authored cues independently from waypoints and coalesce render labels.
// DEPENDENCIES:
//   Core; Level/Player/Hunter; Run; Input/Camera; TagArena SceneRoot;
//   shared test CaptureGateTrace; UnityEditor, NUnit and Unity Test Framework.
// USAGE NOTES:
//   Coordinator owns the Unity lease. Only the initial factory spawn and seed
//   are arranged before tick one. Hunters are disabled. No pose, velocity,
//   geometry, profile, camera setting or time changes occur during collection.
//   Test-owned subscriptions, initial root fields and device filters are paired
//   in Dispose. Each native binary remains an incomplete diagnostic window.
//   Deferred PNG request and subsequent render/file observations are distinct.
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
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Orchestrator;
using Worsen.Presentation.Camera;
using Worsen.Presentation.Input;
using Worsen.Session.Run;
using Worsen.Tests.Player;

namespace Worsen.Tests.Level
{
    public sealed class TagArenaDesignSpeedAcceptanceTests
    {
        private const string Arena = "Assets/Scenes/TagArena.unity";
        private const string Fixture = "Assets/Editor/Tests/Level/TagArenaDesignSpeedAcceptanceTests.cs";
        private const string ProtocolSha256 = "49FDD6DF33F49BE16C0E261BA4A9D8B320E516EC89965F1B02A714D6E705F0DF";
        private const string ProtocolBase64 = "77u/IyBUYWdBcmVuYSBkZXNpZ24tc3BlZWQgd2l0bmVzcyBwcm90b2NvbCAwMjUg4oCUIGNvbWJpbmVkIHByb3NwZWN0aXZlIHdpdG5lc3MKClN0YXR1czogcHJvc3BlY3RpdmUgY29tYmluZWQgY2FuZGlkYXRlIGlkZW50aXR5IGF1dGhvcml6ZWQgYnkgdGhlIGNvb3JkaW5hdG9yOyBmaW5hbCBjb21iaW5lZCBjb21waWxlLCBwdWJsaWNhdGlvbiBwcm92ZW5hbmNlIGFuZCBuYXRpdmUgdmFsaWRhdGlvbiByZW1haW4gcGVuZGluZy4gT25lIGV4cGxpY2l0IGNhcHR1cmUtb25seSBmaXh0dXJlOyByb290IHJldmlld3MgYW5kIGZyZWV6ZXMgYmVmb3JlIHB1YmxpY2F0aW9uLiBUaGlzIGNhcHR1cmUgcGF0Y2ggY29udGFpbnMgbm8gcnVudGltZSwgZ2VvbWV0cnksIG1hcmtlciwgY2FtZXJhIHNldHRpbmcsIHByb2ZpbGUsIHNjZW5lLCBvciBuYXZpZ2F0aW9uIGVkaXRzLiBUaGUgY29vcmRpbmF0ZWQgbmF0aXZlIDAyNSBydW4gbWF5IGluY2x1ZGUgc2VwYXJhdGVseSByZXZpZXdlZCBQbGF5ZXIgY29ycmVjdGlvbnMgdW5kZXIgdGhlIG1hbmRhdG9yeSBwcm92ZW5hbmNlIGFkZGVuZHVtIGJlbG93LgoKIyMgRXhwbGljaXQgY2FwdHVyZS1vbmx5IGRlbHRhIGZyb20gMDI0CgpUaGlzIGRyYWZ0IGRlcml2ZXMgZnJvbSBmcm96ZW4gMDI0IHByb3RvY29sIFNIQTI1NiAzRjU3OTJCNUZDODYwMTE1MUQwNzVCNzE0NTQ4NUVBNDk5OUIwMjFFNzQ0MjUyMEE5N0ZGODU1NTg1RDExQUU3LiBUaGUgdHdlbHZlIHJvdXRlcywgc3RhcnRpbmcgcG9zaXRpb25zLCBpbnB1dCBwb2xpY3ksIG1vdmVtZW50L3BoeXNpY2FsIGFjY2VwdGFuY2UsIHRpbWluZywgY3V0b2ZmcywgbmF0aXZlLWJ5dGUgaGFuZGxpbmcgYW5kIHNvdXJjZSByZXBsYXkgcmVtYWluIGlkZW50aWNhbC4gVGhlIHJldGFpbmVkIDAyNCBydW4gYW5kIGFsbCBpdHMgbWlzc2VzIHJlbWFpbiBpbW11dGFibGUgaGlzdG9yaWNhbCBldmlkZW5jZS4gTm8gcnVudGltZS9jb25maWcvc2NlbmUvZ2VvbWV0cnkgY29ycmVjdGlvbiBpcyBpbmNsdWRlZCBpbiB0aGlzIGNhcHR1cmUgcGF0Y2guIEEgbGF0ZXIgcnVudGltZSBjYW5kaWRhdGUgcmVxdWlyZXMgaXRzIG93biByZXZpZXdlZCBwcm92ZW5hbmNlIGFuZCByZWdyZXNzaW9uIGV2aWRlbmNlLgoKTW92ZSBvYnNlcnZhdGlvbiB0YXJnZXRzIGFyZSBpbmRlcGVuZGVudCBmcm9tIG5hdmlnYXRpb24gZGVzdGluYXRpb25zLiBSZXNvbHZlIHNoYXJlZC0xMDEgdG8gYWN0dWFsIG1hcmtlcjEwMSBhdCAoLTEyLDAsMCksIGxvd2VyLTEwMiB0byBtYXJrZXIxMDIgYXQgKDEyLDAsLTQpLCBhbmQgdXBwZXItMTAzIHRvIG1hcmtlcjEwMyBhdCAoMTIsMCw0KSwgaW4gYm90aCB0cmF2ZWwgZGlyZWN0aW9ucy4gRXhpc3RpbmcgaW50ZXJhY3Rpb24gbGVncyByZXRhaW4gdGhlaXIgYWN0dWFsIG1hcmtlciwgaW5jbHVkaW5nIGRyb3AxMDYgYXQgKDEwLDQuMDgsNCkuIFRoZSByYW1wIGhhcyBubyBkZWRpY2F0ZWQgbWFya2VyOiByYW1wLXRvLWRlY2sgYmluZHMgdGhlIHVuaXF1ZSBhY3R1YWwgY29sbGlkZXIgbmFtZWQgQXRyaXVtIFJpc2luZyBMaW5lLCB3aXRoIGl0cyBjYXB0dXJlZCBjZW50ZXIvYm91bmRzIGFuZCBzb3VyY2UgaWRlbnRpdHkuIFB1cmUgdHVybiwgam9pbiBhbmQgbWljcm9sb29wIHdheXBvaW50cyBhcmUgZXhwbGljaXRseSBuYXZpZ2F0aW9uIG9ic2VydmF0aW9ucyBhbmQgZG8gbm90IGNsYWltIGFuIGF1dGhvcmVkIG1hcmtlciBjdWUuIFNhdmUgdGhlIGNvbXBsZXRlIHJvdXRlIGN1ZSBiaW5kaW5ncyBiZWZvcmUgdGljayBvbmUuCgpLZWVwIHRoZSBleGlzdGluZyBkZXNjcmlwdGl2ZSA2IG0gbmVhcmJ5IHJhbmdlIGFuZCA3LjUgbS9zIG9yZGluYXJ5LXNwZWVkIHJlcXVlc3QgdGhyZXNob2xkLiBGb3IgbWFya2VyIGFuZCByYW1wIG9ic2VydmF0aW9ucywgYW4gYXBwcm9hY2ggbXVzdCBiZSBvbiBvciBiZWZvcmUgdGhlIGN1ZSBwbGFuZSBpbiB0aGUgZGVjbGFyZWQgdHJhdmVsIGRpcmVjdGlvbjsgcmVhY2hpbmcgdGhlIHNwZWVkIHRocmVzaG9sZCBvbmx5IGFmdGVyIHBhc3NpbmcgYSBjdWUgY2Fubm90IGJlIGxhYmVsZWQgaXRzIGFwcHJvYWNoLiBSZXRhaW4gYSBtaXNzaW5nLXNwZWVkLXZpZXcgaWYgbm9ybWFsIHNsb3BlIG1vdGlvbiBvciByb3V0ZSBzdGFydHVwIGRvZXMgbm90IG1lZXQgaXQuIFBvcnRhbCBhbmQgcmFtcC1jZW50ZXIgY3Jvc3NpbmdzIGFyZSBwYXNzaXZlIG9ic2VydmF0aW9ucyB3aGVuIHRoZSByZWNvcmRlZCBwb3NpdGlvbiBwYXNzZXMgdGhhdCBwbGFuZTsgdGhleSBkbyBub3QgY2hhbmdlIHJvdXRlIHByb2dyZXNzLiBSYW1wLWNlbnRlciBjcm9zc2luZyBkb2VzIG5vdCBjbGFpbSB0aGUgc3RhcnQgb2YgdGhlIHJhbXAuIE5hdmlnYXRpb24gYXJyaXZhbHMgcmVtYWluIHRoZSBvcmlnaW5hbCAwLjggbSByZWdpb24vZWxldmF0aW9uL2dyb3VuZGVkIHByZWRpY2F0ZS4gRWFjaCBsYWJlbCByZWNvcmRzIGN1ZSBraW5kLCBhY3R1YWwgc291cmNlL21hcmtlciwgcG9zaXRpb24sIHRyaWdnZXIgcG9zaXRpb24gYW5kIHNpZ25lZCByZWxhdGlvbjsgaXRzIGxlZyBqb2lucyB0aGUgZnJvemVuIGN1ZS1iaW5kaW5ncyByZWNvcmQgZm9yIGFjdHVhbCBib3VuZHMgYW5kIGRlY2xhcmVkIHRyYXZlbCBkaXJlY3Rpb247IHdheXBvaW50IGRlcGFydHVyZSByZW1haW5zIGRpc3RpbmN0IGZyb20gYW4gYXV0aG9yZWQtcGxhbmUgY3Jvc3NpbmcuCgpPbmUgcGh5c2ljYWwgZGVmZXJyZWQgUE5HIHJlcXVlc3QgaXMgYWRtaXR0ZWQgcGVyIHNlbGVjdGVkIG91dHB1dC1jYW1lcmEgcmVuZGVyLCBhbmQgbm8gbW9yZSB0aGFuIG9uZSBpcyBpc3N1ZWQgaW4gdGhlIHNhbWUgVW5pdHkgZnJhbWUuIEFsbCB1bmlxdWUgbGFiZWxzIHF1ZXVlZCBhdCBhZG1pc3Npb24gYXNzb2NpYXRlIHdpdGggdGhhdCBvbmUgcGh5c2ljYWwgaW1hZ2UuIExhYmVscyBhcnJpdmluZyBhZnRlciBhIGNhcHR1cmUgaW4gdGhlIHNhbWUgZnJhbWUgcmVtYWluIHF1ZXVlZCB1bnRpbCB0aGUgbmV4dCBlbGlnaWJsZSBzZWxlY3RlZC1jYW1lcmEgcmVuZGVyLCByZXRhaW5pbmcgdGhlIGFjdHVhbCBkZWxheS4gQ29hbGVzY2VkIGxhYmVscyBhcmUgb25lIHZpZXcsIG5ldmVyIGluZGVwZW5kZW50IHRlbXBvcmFsIGV2aWRlbmNlLiBQcmVzZXJ2ZSBldmVyeSBsYWJlbCB0cmlnZ2VyLCBwaHlzaWNhbC1pbWFnZSBhc3NvY2lhdGlvbiwgcmVxdWVzdCByZW5kZXIsIGZpcnN0IHN1YnNlcXVlbnQgcmVuZGVyIGFuZCBvYnNlcnZlZCBmaWxlIGNvbXBsZXRpb24gc2VwYXJhdGVseS4gRGVwYXJ0dXJlIHRyaWdnZXJQaGFzZSBjYW4gbmFtZSB0aGUgb3V0Z29pbmcgbGVnIHdoaWxlIHRoZSBsYXRlciByZXF1ZXN0LnBoYXNlIG5hbWVzIHRoZSBuZXh0IGxlZyBhdCB0aGUgc2FtZSBjb21taXR0ZWQgdGljazsga2VlcCB0aG9zZSB2YWx1ZXMgZGlzdGluY3QgYW5kIHVzZSBhY3R1YWwgaGVhZGluZy9pbnB1dCB0byBlc3RhYmxpc2ggd2hldGhlciBhIHR1cm4gb2NjdXJyZWQuIE5vIGFkZGl0aW9uYWwgZHVyaW5nLXR1cm4gdmlldyBpcyByZXF1aXJlZCBieSB0aGlzIHBhdGNoLiBSZXBvcnQgbWlzc2luZyBwaHlzaWNhbCBpbWFnZXMgYW5kIHVuaXF1ZSBsYWJlbHMgbmV2ZXIgYXNzaWduZWQgc2VwYXJhdGVseTsgZHVwbGljYXRlZCBsYWJlbHMgcG9pbnQgdG8gdGhlaXIgb3JpZ2luYWwgbGFiZWwgcmVjb3JkLiBNaXNzaW5nL3JlamVjdGVkIGltYWdlIHJlcXVlc3RzIHJlbWFpbiB2aXN1YWwgZ2Fwczsgam91cm5hbC9wcm92ZW5hbmNlL2ZpbmFsLWhhc2ggZmFpbHVyZXMgcmVtYWluIGludGVncml0eSBlcnJvcnMuIE5vIHBhdXNlcywgaW5wdXQgY2hhbmdlcywgZm9yY2VkIHJlbmRlciwgdGFyZ2V0LXRleHR1cmUgb3ZlcnJpZGUgb3IgaW1hZ2Ugc3ludGhlc2lzIGFyZSBpbnRyb2R1Y2VkLgoKIyMgQ29vcmRpbmF0b3IgcnVudGltZSBwcm92ZW5hbmNlIGFkZGVuZHVtIC0gcHJvc3BlY3RpdmVseSBmcm96ZW4gMDI1CgpUaGUgY29vcmRpbmF0b3IgYXV0aG9yaXplZCB0aGlzIGV4YWN0IGNvbWJpbmVkIGNhbmRpZGF0ZSBhZnRlciByZXZpZXdpbmcgdGhlIGZpbmFsIFBsYXllciB2YXVsdCBhbmQgYm91bmRlZCBoZWFkcm9vbS1zdGVwIGNvcnJlY3Rpb25zLiBUaGUgY2FwdHVyZSBmaXh0dXJlJ3MgZnVuY3Rpb25hbCBib2R5LCB0d2VsdmUgcm91dGVzLCBpbnB1dHMsIHBoeXNpY2FsIHByZWRpY2F0ZXMsIGRlc2NyaXB0aXZlIHRocmVzaG9sZHMgYW5kIGNhcHR1cmUgYmF0Y2hpbmcgYXJlIHVuY2hhbmdlZCBmcm9tIGNhcHR1cmUgY2FuZGlkYXRlIDY2RDZBMDJEMkMzRTkwMTUzRjBEQUM1NUYyMzYyQjlFNjEzRTI2MUI4Njk0RTFEOUMxMDY0QzE4RDM0Q0Y4NUEuIFRoYXQgaXMgYSBwcmlvciBjYXB0dXJlLW9ubHkgaWRlbnRpdHksIG5vdCB0aGUgZmluYWwgZml4dHVyZSBoYXNoLgoKRXhwZWN0ZWQgcnVudGltZSBzb3VyY2Ugc3RhbXA6IGBzaGEyNTY6M0FBRDRGMTcwMEM2NDEwNDc2QUI0NjEwMzBBRTlCNTJGNTRGNEU1QzEzNjg5QkUzN0JFRjc3OTlGRkFEM0NFQ2AuIEl0IGlzIGNhbGN1bGF0ZWQgZnJvbSBhbGwgMTUyIHJ1bnRpbWUgQyMgZmlsZXMgaW4gdGhlIGltbXV0YWJsZSBQbGF5ZXIgY29tcGlsZS0wMDMgc25hcHNob3RzOiAxNDkgZmlsZXMgbWF0Y2ggdGhlIHZlcmlmaWVkIDAyNCBiYXNlbGluZSBhbmQgZXhhY3RseSB0aGVzZSB0aHJlZSBoYXZlIHRoZSBhcHByb3ZlZCBuZXcgYnl0ZXM6Cgp8IFJ1bnRpbWUgc291cmNlIHwgU0hBMjU2IHwKfCAtLS0gfCAtLS0gfAp8IEFzc2V0cy9TY3JpcHRzL0RvbWFpbi9QbGF5ZXIvQ29udHJvbGxlci9QbGF5ZXJDb250cm9sbGVyLmNzIHwgYEU3NkVDNEQ4RTJDQzhEMDA5RDFDREY4NjEwNzc2MjE4NEVDMUE0RkM3NEMxMjk4MjNGMkNERUIwQTc4RkI1ODNgIHwKfCBBc3NldHMvU2NyaXB0cy9Eb21haW4vUGxheWVyL0RyaXZlci9QbGF5ZXJNb3ZlclByZXNlbnRlci5jcyB8IGBCOEUxNTRFRTAzMDVFMzUzNkU5RUMxMTI5RkRGMTE3NUFGNzI1MjQyNEU2QTk4ODYwMzQ0MDg5NjVFNEY0Q0VFYCB8CnwgQXNzZXRzL1NjcmlwdHMvRG9tYWluL1BsYXllci9Ecml2ZXIvUGxheWVyRHJpdmVyLmNzIHwgYDU4OUZGQzFDRDRFRTBDRjk5NTY1MjlGNzJBMzhGMjg4RkNFQkVBM0E5NzVEQzVEQjUxQUVENUI1MzI0QjFCMTJgIHwKClRoZSBzb3VyY2UgbWFuaWZlc3QgaXMgW1BsYXllciBjb21waWxlLTAwM10oLi4vLi4vLi4vUExBTi0wMDMvdmVyaWZpY2F0aW9uL3ZhdWx0LTAyNS9jb21waWxlLTAwMy9zb3VyY2UtbWFuaWZlc3QuanNvbikgd2l0aCBTSEEyNTYgYEUxMkZBM0U1Q0FGMDJEMjE3NjRGOUNGNzQwRDdGQTlCQkY5QTRGRTBBOEQ0MUI3RTk4ODk0MTAwNkVBQzk3M0FgLiBJdCBwcmVjZWRlcyBmaW5hbCBjYXB0dXJlIHByb3RvY29sIGVtYmVkZGluZzsgaXRzIHJ1bnRpbWUgaW5wdXRzIGFyZSBjb21wbGV0ZSwgd2hpbGUgaXRzIGNhcHR1cmUtdGVzdCBtZXRhZGF0YSBpcyBub3QgY2xhaW1lZCB0byBiZSB0aGUgZmluYWwgY29tYmluZWQgZml4dHVyZS4gVGhlIHJ1bnRpbWUgc3RhbXAgYWxnb3JpdGhtIHVzZXMgb3JpZ2luYWwgV2luZG93cyBwYXRoIG9yZGluYWwgc29ydGluZywgc2xhc2gtbm9ybWFsaXplZCBwYXRoICsgQ1JMRiArIEZpbGUuUmVhZEFsbFRleHQgY29udGVudHMgdW5jaGFuZ2VkICsgQ1JMRiwgdGhlbiBVVEYtOCBTSEEyNTYuIEVkaXRvci90ZXN0L3Byb3RvY29sIGJ5dGVzIGFyZSBleGNsdWRlZC4KCkFsbCAxOCBjb25maWd1cmF0aW9uIGFzc2V0cyByZW1haW4gYnl0ZS1pZGVudGljYWwgdG8gMDI0OyBleHBlY3RlZCBjb25maWd1cmF0aW9uIHN0YW1wIGlzIGBzaGEyNTY6RDUwRUMxNUFFNzY2MDQ2RjUyRkMwMDY4OTIyQTJBQTkzOEQ3QTBGM0MzMTMwQUMwMDAxMTlDOTdBNDU4MEY4RGAuIEFsbCBlaWdodCBzY2VuZS9uYXZpZ2F0aW9uL21ldGFkYXRhIGZpbGVzIG1hdGNoIHRoZSAwMjQgYmFzZWxpbmUgYmVmb3JlIHB1YmxpY2F0aW9uLiBSZXZpc2lvbjAwMiBleHBsaWNpdGx5IGF1dGhvcml6ZXMgb25seSB0aGUgc2VyaWFsaXplZCBgX3NvdXJjZVJldmlzaW9uYCBzdHJpbmcgaW4gYm90aCBUYWdBcmVuYSBhbmQgRmxvb3JMb29wIFNjZW5lUm9vdHMgdG8gY2hhbmdlIGZyb20gYHNoYTI1NjowN0VFMjg2NzVDODZGRjBBNjJGMjUxN0YzMURDRDcwNDc5M0NENTBBODA0OTQwNUZFMDg1OEI1REM5RERBNzZCYCB0byBgc2hhMjU2OjNBQUQ0RjE3MDBDNjQxMDQ3NkFCNDYxMDMwQUU5QjUyRjU0RjRFNUMxMzY4OUJFMzdCRUY3Nzk5RkZBRDNDRUNgLiBUaGUgY29vcmRpbmF0b3Igd2lsbCBhcHBseSB0aGlzIG5hcnJvdyBTZXJpYWxpemVkT2JqZWN0IHVwZGF0ZSB1bmRlciB0aGUgVW5pdHkgbGVhc2UgYW5kIHNhdmUgb25seSB0aGUgaW50ZW5kZWQgY2xlYW4gc2NlbmVzLiBgX2NvbmZpZ1NuYXBzaG90SGFzaGAsIGdlb21ldHJ5LCB3aXJpbmcsIG5hdmlnYXRpb24gYXNzZXRzIGFuZCBtZXRhZGF0YSBHVUlEcyByZW1haW4gdW5jaGFuZ2VkOyBubyBmdWxsIHNjZW5lIHJlYnVpbGQgaXMgcmVxdWlyZWQgZm9yIHRoZXNlIHNhbWUtY29udHJhY3QgUGxheWVyIGNvcnJlY3Rpb25zLgoKVGhlIFtwcm9zcGVjdGl2ZSBzY2VuZSBwcm92ZW5hbmNlIHJlY29yZF0oLi4vLi4vLi4vLi4vQWdlbnRWYWxpZGF0aW9uL0dvYWxDb21wbGV0aW9uL2Rlc2lnbi1zcGVlZC0wMjUvc2NlbmUtcHJvdmVuYW5jZS9wcm9zcGVjdGl2ZS5qc29uKSBhbmQgcHJlc2VydmVkIGJlZm9yZS9leHBlY3RlZCBjb3BpZXMgZXN0YWJsaXNoIHRoZXNlIGV4YWN0IGJ5dGUgdGFyZ2V0czoKCnwgU2NlbmUgfCBCZWZvcmUgU0hBMjU2IHwgUHJvc3BlY3RpdmUgcG9zdHN0YW1wIFNIQTI1NiB8CnwgLS0tIHwgLS0tIHwgLS0tIHwKfCBBc3NldHMvU2NlbmVzL1RhZ0FyZW5hLnVuaXR5IHwgYEVDOUUwQUU2MTJBNTA0RjlGRDM2MEUwMkRGOUVDNzhDQzg2MzA5RjU0REIyN0U4QjgzMzg5MUE4NEFBMzdFQ0NgIHwgYDc5OEIyMDQ3MzhCNkRDREFDMDhFQjQ2QkQ2RTgxNjFEMThGMkVERDUwRkQyNzg2NUMxNjI4N0YwNjIyRjUwRDlgIHwKfCBBc3NldHMvU2NlbmVzL0Zsb29yTG9vcC51bml0eSB8IGAzMzQ2NEZFNkVGNTVFNThCMTBBRkRDQjMyNUUzRjJFRTY4NEZCMjhCMzk4MDE1RDJGNDBGREYxRjdGQTlDNzZGYCB8IGAxMjlFNDZDMzBDQUM2NjI5NTFFRkZFRkI0MjdEMTZEOTU3RDFGMzU1MDMwRTU0QTZFQzAzMUE2MDJENTIzQUYyYCB8CgpUaGVzZSBhcmUgcHJvc3BlY3RpdmUgZXhwZWN0ZWQgc2NlbmUgaGFzaGVzLCBub3QgYSBjbGFpbSB0aGF0IHB1YmxpY2F0aW9uIGhhcyBvY2N1cnJlZC4gQWZ0ZXIgc2F2aW5nLCB0aGUgY29vcmRpbmF0b3IgbXVzdCB2ZXJpZnkgZXhhY3QgcG9zdHN0YW1wIGJ5dGVzLCB1bmNoYW5nZWQgbmF2aWdhdGlvbi9jb25maWd1cmF0aW9uL21ldGEgYXNzZXRzLCBhbmQgdGhlIGFjdHVhbCBzY2VuZSBkZXBlbmRlbmN5IGhhc2ggYmVmb3JlIGFueSBuYXRpdmUgcnVuLiBJZiBzYXZlIGJ5dGVzIGRpZmZlciBmcm9tIHRoZSBkZWNsYXJlZCBzdHJpbmctb25seSByZXBsYWNlbWVudCwgcmV0YWluIGFuZCByZXZpZXcgdGhlIGRyaWZ0IGJlZm9yZSBwcm9jZWVkaW5nLiBUaGUgZXhpc3Rpbmcgc291cmNlL2NvbmZpZ3VyYXRpb24gYWRtaXNzaW9uIGNoZWNrcyByZW1haW4gZW5hYmxlZC4gUmVjb3JkIGFjdHVhbCBwb3N0LXNhdmUgaWRlbnRpdGllcyBhbmQgbmF0aXZlIHJlc3VsdHMgaW4gdGhlIGV4dGVybmFsIGZyZWV6ZSBtYW5pZmVzdCB3aXRob3V0IGNoYW5naW5nIHRoaXMgZW1iZWRkZWQgcHJvdG9jb2wuCgpFdmlkZW5jZTogW3NvdXJjZSByZXZpZXddKC4uLy4uLy4uLy4uL0FnZW50VmFsaWRhdGlvbi9Hb2FsQ29tcGxldGlvbi9kZXNpZ24tc3BlZWQtMDI1L1NPVVJDRS1SRVZJRVcubWQpLCBbdmF1bHQgZGVzaWduXSguLi8uLi8uLi8uLi9BZ2VudFZhbGlkYXRpb24vR29hbENvbXBsZXRpb24vZGVzaWduLXNwZWVkLTAyNS92YXVsdC1kZXNpZ24vUkVBRE1FLm1kKSwgW2FjdHVhbCBkcm9wLWNvbnRhY3QgYXVkaXRdKC4uLy4uLy4uLy4uL0FnZW50VmFsaWRhdGlvbi9Hb2FsQ29tcGxldGlvbi9kZXNpZ24tc3BlZWQtMDI1L2Ryb3AtZGVzaWduL05BVElWRS1BVURJVC5tZCksIFtzZXBhcmF0ZWx5IGxhYmVsZWQgY291bnRlcmZhY3R1YWwtMDAzXSguLi8uLi8uLi8uLi9BZ2VudFZhbGlkYXRpb24vR29hbENvbXBsZXRpb24vZGVzaWduLXNwZWVkLTAyNS9kcm9wLWRlc2lnbi9jb3VudGVyZmFjdHVhbC0wMDMvUkVBRE1FLm1kKSwgYW5kIFtzaXgtZmlsZSBQbGF5ZXIgaGFuZG9mZl0oLi4vLi4vLi4vUExBTi0wMDMvdmVyaWZpY2F0aW9uL3ZhdWx0LTAyNS92YXVsdC1zdGVwLWhhbmRvZmYtMDAyLmpzb24pLiBTb3VyY2UgcmV2aWV3IGFuZCB0aGUgUGxheWVyIHNldmVuLWFzc2VtYmx5IGNvbXBpbGUvbGludCBwYXNzIGFyZSBjb21wbGV0ZS4gRmluYWwgY29tYmluZWQgY29tcGlsZSwgcHVibGlzaGVkLWNvZGUgbmF0aXZlIHJlZ3Jlc3Npb25zLCBuYXRpdmUgMDI1IHJvdXRlIGNvbXBsZXRpb24gYW5kIGFjdHVhbCBQTkcvcmVhZGFiaWxpdHkgdmVyaWZpY2F0aW9uIGFyZSBleHBsaWNpdGx5IFBFTkRJTkcuIFRoZSBjb3VudGVyZmFjdHVhbCBhbmQgb2ZmbGluZSBjaGVja3MgZG8gbm90IHN1YnN0aXR1dGUgZm9yIHRob3NlIG5hdGl2ZSByZXN1bHRzLgoKVGhlIGZpbmFsIGZpeHR1cmUvcHJvdG9jb2wgcGFpciBpcyByZWNvcmRlZCBvbmx5IGluIHRoZSBzZXBhcmF0ZSBjb29yZGluYXRvciBbZnJlZXplLW1hbmlmZXN0Lmpzb25dKGZyZWV6ZS1tYW5pZmVzdC5qc29uKSwgYWZ0ZXIgZW1iZWRkaW5nLiBUaGlzIHByb3RvY29sIGRvZXMgbm90IGluY2x1ZGUgaXRzIG93biBmaW5hbCBoYXNoIG9yIHRoZSBmaW5hbCBmaXh0dXJlIGhhc2guIEFueSBjYW5kaWRhdGUgc291cmNlLWJ5dGUgY2hhbmdlIHJlcXVpcmVzIGNvb3JkaW5hdG9yIHJldmlldyBhbmQgcmVpc3N1ZSBvZiB0aGUgcHJvc3BlY3RpdmUgaWRlbnRpdHk7IHRoZSBmaW5hbCBjb21iaW5lZCBjb21waWxlIGFuZCBwdWJsaWNhdGlvbi9uYXRpdmUgcmVjb3JkcyBiZWxvbmcgaW4gdGhhdCBleHRlcm5hbCBtYW5pZmVzdCwgYXZvaWRpbmcgY2lyY3VsYXIgaGFzaGVzLgoKIyMgUHVycG9zZSBhbmQgbGltaXRzCgpQTEFOLTAwNCByZXF1aXJlcyB0aGUgYWN0dWFsIGF1dGhvcmVkIHRyYXZlcnNhbCBsaW5lcyB0byB3b3JrIHVuZGVyIG9yZGluYXJ5IGRlc2lnbi1zcGVlZCBtb3ZlbWVudCBhbmQgdGhlaXIgY3VlcyB0byBiZSBpbnNwZWN0YWJsZSBmcm9tIHRoZSBhY3R1YWwgUGxheWVyIGNhbWVyYS4gVGhlIGZpeHR1cmUgc3VwcGxpZXMgZnVsbCBub3JtYWxpemVkIG1vdmVtZW50IHdpdGggZGVmYXVsdCBhdXRvLXNwcmludCwgbmV2ZXIgdGhlIHByZWNpc2lvbi9TcHJpbnQgbW9kaWZpZXIsIG5ldXRyYWwvYnJha2luZyBwYXVzZXMsIHJlcGVhdGVkIGFydGlmaWNpYWwgYm9vc3RzLCBwb3NlL3ZlbG9jaXR5IHdyaXRlcywgb3IgZmFicmljYXRlZCBwcm9iZXMuIFRoZSBleGlzdGluZyBNMSBwb29sZWQgc3BlZWQgcmVzdWx0IGlzIHNlcGFyYXRlLiBObyBwZXItcm91dGUgcGVyY2VudGlsZSwgdW5pZm9ybSA4IG0vcyBmbG9vciwgZm9yY2VkIDE0IG0vcyBjYXNlLCBpbnN0YW50YW5lb3VzLXR1cm4gcmVxdWlyZW1lbnQsIHBhcnRpY2lwYW50IGp1ZGdtZW50LCBvciBIdW50ZXItb25seSBQbGF5ZXIgcGFzc2FnZSBpcyBhZGRlZC4KClRoZSBleGlzdGluZyBjb25uZWN0ZWQtcm91dGUgdHJhY2UgY3JlZGl0cyByZWFsIGNvbGxpc2lvbnMgYnV0IGRlbGliZXJhdGVseSBzbG93cy9zdG9wcyBhdCB3YXlwb2ludHMgYW5kIGFmdGVyIHZhdWx0IHJlcXVlc3RzOyB0aGVyZWZvcmUgdGhpcyBib3VuZGVkIG5ldyBjb2xsZWN0aW9uIHN1cHBsaWVzIGNvbnRpbnVvdXMgbW92ZW1lbnQgYW5kIGNhbWVyYSBldmlkZW5jZS4gRXhpc3RpbmcgSHVudGVyIGNvcnJpZG9yIDEwNCAvIGxpbmtzIDEwN+KAkzEwOCBib3RoLWRpcmVjdGlvbiBuYXZpZ2F0aW9uL3BoeXNpY2FsIHRyYXZlbCBhbmQgUGxheWVyIHJlZnVzYWwgcmVtYWluIGNyZWRpdGVkIHNlcGFyYXRlbHkuIFRoZSBzaW5nbGUtdGFyZ2V0IG1hcmtlciAyMDEgaXMgdGVzdGVkIHNvdXRoLXRvLW5vcnRoIG9ubHkuIFJlYm91bmQgMjAyIGlzIHRlc3RlZCBvbmx5IGF0IGl0cyBzdHJpcGVkIHdlc3QvZWFzdCBmYWNlcy4gVGhlIGxvbmcgZGlhZ29uYWwgaXMgYSBnZW9tZXRyeSByYXkgd2l0bmVzcywgbm90IGFuIGV4dHJhIHRyYXZlcnNhbCB2ZXJiLgoKIyMgRnJvemVuIGZpbml0ZSBhdHRlbXB0cwoKRWFjaCByb3cgZ2V0cyBleGFjdGx5IG9uZSBmcmVzaCBzZWVkLTEgYXV0aG9yZWQgVGFnQXJlbmEgc2NlbmUgYW5kIGl0cyByZWFsIFBsYXllciBmYWN0b3J5IGF0IHRoZSBzdGF0ZWQgaW5pdGlhbCBmbG9vciBwb3NpdGlvbiAoeT0uMDUgdW5sZXNzIHNwZWNpZmllZCksIHdpdGggemVybyBpbml0aWFsIHZlbG9jaXR5LiBJbml0aWFsIGhlYWRpbmcgaXMgdGhlIGZpcnN0IGxlZydzIGJlYXJpbmcgdGhyb3VnaCB0aGUgZmFjdG9yeSdzIGV4aXN0aW5nIHNwYXduIHJlcXVlc3QgKGlmIHRoZSBmYWN0b3J5IGZpeGVzIGhlYWRpbmcgYXQgOTDCsCwgcHJlc2VydmUgdGhhdCB2YWx1ZSBhbmQgbGV0IG9yZGluYXJ5IGxvb2sgaW5wdXQgdHVybiBmcm9tIHRpY2sgb25lKS4gQWxsIHBvc2l0aW9ucyBiZWxvdyBhcmUgKHgseiksIHdpdGggZWxldmF0aW9uIGV4cGxpY2l0bHkgaW5kaWNhdGVkLiBObyByZXN0YXJ0L3JldHJ5IGFmdGVyIGEgcm91dGUgbWlzcyB3aXRoaW4gdGhpcyBwcm90b2NvbC4KCnwgQXR0ZW1wdCB8IFN0YXJ0IHwgQ29udGludW91cyBvcmRlcmVkIHJvdXRlIC8gc3RvcCB8IFJlcXVpcmVkIGV2aWRlbmNlIHwKfCAtLS0gfCAtLS0gfCAtLS0gfCAtLS0gfAp8IGxvdy1sb3dlci1lYXN0IHwgKC0yMSwtMSkgfCAoLTEwLC0xKSwgKC05LC00KSwgKDE2LC00KSB8IFNoYXJlZCAxMDEgdGhlbiBsb3dlciBicmFpZCAxMDIsIGFsbCB3YXlwb2ludHMgZ3JvdW5kZWQgfAp8IGxvd2VyLWxvdy13ZXN0IHwgKDE2LC00KSB8ICgtOSwtNCksICgtMTAsLTEpLCAoLTIxLC0xKSB8IExvd2VyIDEwMiB0aGVuIHNoYXJlZCAxMDEsIGFsbCB3YXlwb2ludHMgZ3JvdW5kZWQgfAp8IHVwcGVyLWVhc3QgfCAoMCw0KSB8ICgxNiw0KSB8IFVwcGVyIGJyYWlkIDEwMywgZ3JvdW5kZWQgfAp8IHVwcGVyLXdlc3QgfCAoMTYsNCkgfCAoMCw0KSB8IFVwcGVyIGJyYWlkIDEwMywgZ3JvdW5kZWQgfAp8IG1pY3JvbG9vcC1jbG9ja3dpc2UgfCAoLTksLTIpIHwgKC0xLC0yKSwgKC0xLDgpLCAoLTksOCksICgtOSwtMikgfCBCb3RoIHNpZGVzIG9mIHBpbGxhciAyMDIgLyBMb3NCcmVhayAyMDMgd2l0aG91dCBjb250YWN0IHNob3J0Y3V0IHwKfCBtaWNyb2xvb3AtY291bnRlcmNsb2Nrd2lzZSB8ICgtOSwtMikgfCAoLTksOCksICgtMSw4KSwgKC0xLC0yKSwgKC05LC0yKSB8IE9wcG9zaXRlIG9yZGVyZWQgbG9vcCwgc2FtZSBhY3R1YWwgZ2VvbWV0cnkgfAp8IHZhdWx0MjAxLXNvdXRoIHwgKC00LC05KSB8IFN0cmFpZ2h0IG5vcnRoLCBhY3R1YWwgZWxpZ2libGUgMjAxIEp1bXAgZWRnZTsgc3RvcCBmaXJzdCBncm91bmRlZCB6Pj0tMi41IGFmdGVyIHN1Y2Nlc3NmdWwgVmF1bHQgfCBUYXJnZXQgZXhhY3RseSAoLTQsMCwtNC41KTsgb25lIHN1Y2Nlc3NmdWwgVmF1bHQgfAp8IHJlYm91bmQyMDItd2VzdCB8ICgtMTEsMykgfCBFYXN0IHRvIHN0cmlwZWQgd2VzdCBmYWNlOyBvbmUgb3JkaW5hcnkgZ3JvdW5kIEp1bXAgYXQgPD0yLjJtIGZyb20gZmFjZSwgdGhlbiBvbmUgSnVtcCBlZGdlIHdpdGggYWN0dWFsIGVsaWdpYmxlIGFpcmJvcm5lIDIwMiB3YWxsIHByb2JlOyBmdWxsIHdlc3QgYWZ0ZXIgUmVib3VuZDsgc3RvcCBmaXJzdCBncm91bmRlZCB4PD0tMTAuNSBhZnRlciBzdWNjZXNzZnVsIHJlYm91bmQgfCBHcm91bmQgSnVtcCwgYWlyYm9ybmUgMjAyIFJlYm91bmQsIGdyb3VuZGVkIGRlcGFydHVyZSB8CnwgcmVib3VuZDIwMi1lYXN0IHwgKDEsMykgfCBXZXN0IHRvIHN0cmlwZWQgZWFzdCBmYWNlOyBzYW1lIGdyb3VuZCBKdW1wIC8gZWxpZ2libGUgYWlyYm9ybmUgUmVib3VuZCBwb2xpY3k7IGZ1bGwgZWFzdCBhZnRlciBSZWJvdW5kOyBzdG9wIGZpcnN0IGdyb3VuZGVkIHg+PTAuNSB8IFNhbWUgb24gZWFzdCBzdHJpcGVkIGZhY2UgfAp8IGNsdXR0ZXItZWFzdCB8ICgtMjEsNSkgfCAoLTIxLDE1KSwgc3RyYWlnaHQgZWFzdCB0aHJvdWdoIDIxMCwyMjAsMjExLDIyMSwyMTIsICgyNywxNSksICgyNyw4LjUpIHwgRXZlcnkgcGFpcmVkIHZhdWx0IHRhcmdldCBlYXN0OyBnYXRlIGNhcHN1bGUvc3RhbmRpbmcgb2JzdHJ1Y3Rpb24gY3Jvc3Npbmc7IGJvdGggam9pbnMgfAp8IGNsdXR0ZXItd2VzdCB8ICgyNyw4LjUpIHwgKDI3LDE1KSwgc3RyYWlnaHQgd2VzdCB0aHJvdWdoIDIxMiwyMjEsMjExLDIyMCwyMTAsICgtMjEsMTUpLCAoLTIxLDUpIHwgRXZlcnkgcGFpcmVkIHZhdWx0IHRhcmdldCB3ZXN0OyBnYXRlIGNyb3NzaW5nOyBib3RoIGpvaW5zIHwKfCB2ZXJ0aWNhbC1kcm9wIHwgKDEzLC02KSB8IFJhbXAgdG8gKDI2LC02LHk0KSwgKDI2LDQseTQpLCAoMjIsNCx5NCksIHdlc3QgYWxvbmcgYnJpZGdlIHBhc3QgbGlwMTA2OyBjb250aW51ZSB3ZXN0IHRocm91Z2ggbmF0dXJhbCBmaXJzdCBncm91bmRlZCBsYW5kaW5nIGF0IHl+MCB8IFJhbXAvZGVjay9icmlkZ2Ugb3JkZXJpbmcsIGFjdHVhbCBsaXAgYXBwcm9hY2gsIGFpcmJvcm5lIHdlc3Qgb2YgbGlwIHRoZW4gTGFuZDsgbm8gdGFyZ2V0aW5nIGEgZm9yY2VkIGxhbmRpbmcgeCB8CgpSb290IGFscmVhZHkgdmVyaWZpZWQgYm90aCBkaXJlY3RlZCBzdGF0aWMgcmF5cyBpbiB0aGUgYWN0dWFsIGVkaXQtbW9kZSBUYWdBcmVuYTsgcmV0YWluZWQgcmVxdWVzdC9kZWNvZGVkIHJlc3BvbnNlcyBhcmUgYXQgTG9ncy9BZ2VudFZhbGlkYXRpb24vUExBTi0wMDQvMjRmYzBhZWUtMmM0NS00ZWNiLTkxYWEtMzAyNzQwYThkYjhjL3NpZ2h0bGluZS1yZWFkLy4gVGhpcyBleGlzdGluZyB3aXRuZXNzIGlzIGNyZWRpdGVkIHNlcGFyYXRlbHksIG5vdCBhbm90aGVyIGdhbWVwbGF5IHJvdy4gVGhlIGxvbmcgZGlhZ29uYWwgc3RhdGljIHJheSBpcyBmcm9tICgtMTEsMS42LC04KSB0byAoMTEsMS42LDgpOyBzYXZlIHJheS9hbGwgYmxvY2tlcnMgYW5kIGdlb21ldHJ5IGlkZW50aXR5IGJlZm9yZSB0aWNrIG9uZS4gQ2FtZXJhIGV2aWRlbmNlIGZyb20gbW92aW5nIHJvb20gcm91dGVzIHJlbWFpbnMgZGlzdGluY3QgZnJvbSB0aGF0IHJheS4gSWYgdGhpcyBwYXJ0aWN1bGFyIHJheSBpcyBibG9ja2VkLCByZXRhaW4gdGhlIGJsb2NrZXI7IGRvIG5vdCBjaGFuZ2UgZW5kcG9pbnRzIGFmdGVyIGNvbGxlY3Rpb24uCgojIyBEZXRlcm1pbmlzdGljIGlucHV0IGFuZCByb3V0ZSBwcm9ncmVzcwoKSW5wdXQgaXMgaW5qZWN0ZWQgdGhyb3VnaCBleGlzdGluZyBJbnB1dE1hbmFnZXIuRnJhbWVQdWJsaXNoZWQgLT4gUnVuLlJlY2VpdmVJbnB1dDsgdGhlIHJlYWwgU2Vzc2lvbiA2MCBIeiB0aWNrIGRyaXZlcyBQbGF5ZXJNYW5hZ2VyL0RyaXZlciBhbmQgY2FtZXJhIG9yY2hlc3RyYXRpb24uIEhhcmR3YXJlIGFjdGlvbi1tYXAgZGV2aWNlcyBhcmUgdGVtcG9yYXJpbHkgZW1wdHkgYW5kIGV2ZXJ5IHBoeXNpY2FsIHByb2R1Y2VyIGZyYW1lIG11c3QgYmUgbmV1dHJhbC4gUmVzdG9yZSBkZXZpY2UgZmlsdGVycyBhZnRlcndhcmQuIERpc2FibGUgSHVudGVycyBiZWZvcmUgdGljayBvbmU7IG5vIGNoYXNlL2Zsb29yIG9wcG9zaXRpb24gaXMgYXNzZXJ0ZWQuCgpBdCBldmVyeSBjYXB0dXJlZCB0aWNrIHN1cHBseSBub3JtYWxpemVkIGhvcml6b250YWwgbW92ZW1lbnQgdG93YXJkIHRoZSBjdXJyZW50IGZpeGVkIHdheXBvaW50OyB5YXcgbW92ZXMgYXQgbW9zdCAxNSBkZWdyZWVzIHBlciB0aWNrIHRvd2FyZCB0aGF0IGRpcmVjdGlvbiBhbmQgbW92ZW1lbnQgaXMgZXhwcmVzc2VkIGluIHRoZSByZXN1bHRpbmcgaGVhZGluZyBiYXNpcy4gQWR2YW5jZSBhIHdheXBvaW50IHdoZW4gcmVzb2x2ZWQgaG9yaXpvbnRhbCBkaXN0YW5jZSA8PTAuOG0gQU5EIGVsZXZhdGlvbiBpcyB3aXRoaW4gLjM1bSBBTkQgZ3JvdW5kZWQsIHdpdGhvdXQgbmV1dHJhbGl6aW5nIGlucHV0OyB0aGUgbmV4dCB0YXJnZXQgdGFrZXMgZWZmZWN0IG9uIHRoZSBuZXh0IHRpY2suIEZpbmFsIHN0b3Agb2NjdXJzIGF0IGZpcnN0IHF1YWxpZnlpbmcgY3Jvc3Npbmcvd2F5cG9pbnQgYW5kIHN1c3BlbmRzIHRpY2tpbmcgYXQgdGhhdCBjb21wbGV0ZWQgdGljay4gVGhlIHdheXBvaW50IHJhZGl1cyBpcyBhbiBhcnJpdmFsIHJlZ2lvbiwgbm90IGEgY2xhaW0gb2YgZXhhY3QgdHJhdmVyc2FsIHRocm91Z2ggaXRzIGNlbnRlci4KClZhdWx0IGxlZ3MgcmVtYWluIGZ1bGwgbW92ZW1lbnQgdGhyb3VnaCB0aGUgZW50aXJlIG9wZXJhdGlvbi4gUmVxdWVzdCBleGFjdGx5IG9uZSBKdW1wIHdoZW4gdGhlIGxhc3QgYWN0dWFsIHByb2JlIG1hdGNoZXMgdGhlIGV4cGVjdGVkIG1hcmtlciB0YXJnZXQgKHdpdGhpbiAuMDFtKSwgcmVwb3J0cyBhIHZhdWx0IGNhbmRpZGF0ZSB3aXRoIHBvc2l0aXZlIGNsZWFyYW5jZSBhbmQgbm8gc3RhbmRpbmcgb2JzdHJ1Y3Rpb247IG5vIHNlY29uZCByZXF1ZXN0IGFmdGVyIGZhaWx1cmUuIEFkdmFuY2Ugb25seSBvbiB0aGUgc3VjY2Vzc2Z1bCBWYXVsdCBmYWN0IGFuZCBtYXRjaGluZyByZXNvbHZlZCBlbmRwb2ludCB3aXRoaW4gdGhlIHByb2ZpbGUncyBjb21wbGV0aW9uIHRvbGVyYW5jZS4gRm9yIGNsdXR0ZXIsIGVhY2ggZ2F0ZSByZXF1ZXN0cyBvbmUgQ3JvdWNoIGF0IDw9Mm0gdG8gaXRzIG5lYXIgZmFjZSwgZ3JvdW5kZWQsIGF0IGxlYXN0IHRoZSBjb25maWd1cmVkIG1pbmltdW0gc2xpZGUgc3BlZWQgbWludXMgMWUtNSBmbG9hdGluZy1wb2ludCB0b2xlcmFuY2UuIEhvbGQgQ3JvdWNoIHVudGlsIHRoZSBjYXBzdWxlIGhhcyBjcm9zc2VkIHRoZSBmYXIgZmFjZSBieSByYWRpdXMrLjFtLiBSZXF1aXJlIGEgZ3JvdW5kZWQgbG93IGNhcHN1bGUgdW5kZXIgdGhlIGFjdHVhbCBnYXRlIGFuZCBhbiBvYnNlcnZlZCBzdGFuZGluZy1ibG9ja2VkIHByb2JlLiBSZWxlYXNlIGNyb3VjaCBhZnRlcndhcmQ7IGRvIG5vdCBzbGlkZS1qdW1wIG9yIHJlYm9vc3QgdG8gb2J0YWluIGEgcGFzcy4gQWxsIGZhaWxlZCB0cmF2ZXJzYWwgZmFjdHMgYW5kIG5vbi1MYW5kIHZlcmJzIG91dHNpZGUgdGhlIG9yZGVyZWQgZGVjbGFyZWQgSnVtcC9WYXVsdC9TbGlkZS9SZWJvdW5kIHNlcXVlbmNlIHJlbWFpbiB2aXNpYmxlIGFuZCBtYWtlIHJvdXRlIGFjY2VwdGFuY2UgZmFsc2UuIE5hdHVyYWwgc3VjY2Vzc2Z1bCBMYW5kIGZhY3RzIGFyZSBwZXJtaXR0ZWQgdGhyb3VnaG91dC4KCkF0IGVhY2ggcmVib3VuZCBmYWNlIHRoZSBpbml0aWFsIEp1bXAgaXMgYSBub3JtYWwgZ3JvdW5kIEp1bXA7IFJlYm91bmQgd2FpdHMgdW50aWwgQWlyLCBhY3R1YWwgd2FsbCBJRDIwMiwgZGlzdGFuY2UgPD0gY29uZmlndXJlZCBSZWJvdW5kRGlzdGFuY2UgYW5kIGFuZ2xlIDw9IGNvbmZpZ3VyZWQgUmVib3VuZEFuZ2xlIHdpdGggdGhlIEp1bXAgZWRnZSByZWxlYXNlZC4gVGhlIGRpcmVjdGlvbiByZXZlcnNlcyBvbmx5IGFmdGVyIGEgc3VjY2Vzc2Z1bCBSZWJvdW5kIGZhY3QuIE5vIHJlcGVhdGVkIHdhbGwgYXR0ZW1wdHMuCgpNYXhpbXVtIDI0MDAgY29tbWl0dGVkIHRpY2tzIHBlciByb3V0ZSwgNDgwIHRpY2tzIHBlciBvcmRlcmVkIGxlZywgOTAgY29uc2VjdXRpdmUgdGlja3Mgd2l0aG91dCA+PS4wNW0gY3VtdWxhdGl2ZSBkaXNwbGFjZW1lbnQgaW4gcmVzb2x2ZWQgM0QgcG9zaXRpb24gZnJvbSB0aGUgbGFzdCBwcm9ncmVzcyBhbmNob3IgKG5vdCBhIHBlci10aWNrIGRpc3BsYWNlbWVudCB0aHJlc2hvbGQpLCBvciBmaXJzdCBiZWxvdy1mbG9vciB5PC0uNW0gZW5kcyB0aGF0IHJvdXRlIGFzIGEgcGh5c2ljYWwgbWlzcy4gVGhlc2UgcHJlZGVjbGFyZWQgZmluaXRlIHN0b3AgcnVsZXMgZG8gbm90IHR1cm4gc2xvdyBtb3ZlbWVudCBpbnRvIGFuIGludGVncml0eSBmYWlsdXJlLiBBbGwgdHdlbHZlIGF0dGVtcHRzIGNvbnRpbnVlIGFmdGVyIGEgcGh5c2ljYWwvbnVtZXJpYy9yZW5kZXIgbWlzcy4gQSA5MC1zZWNvbmQgd2FsbC1jbG9jayBkZWFkbGluZSBhZnRlciByZWFkaW5lc3MsIGludmFsaWQvbWlzc2luZyB0aWNrL3Byb3ZlbmFuY2UsIHN0cmVhbSBmYWlsdXJlIG9yIGxvc3Qgc291cmNlIGlkZW50aXR5IGlzIGFuIGludGVncml0eSBmYWlsdXJlIGFuZCBzdG9wcyBzdWJzZXF1ZW50IGF0dGVtcHRzLiBObyByZXBsYXkgaXMgY291bnRlZCBhcyBhbm90aGVyIHBoeXNpY2FsIGF0dGVtcHQuCgojIyBFdmlkZW5jZSBhbmQgc2VwYXJhdGUgdmVyZGljdHMKCkF1dG8tZmx1c2hlZCB0aWNrcy5qc29ubCByZXRhaW5zIGV2ZXJ5IGNvbW1pdHRlZCBJbnB1dFByb2JlUmVjb3JkLCBzdXBwbGllZCBmcmFtZSwgbW92ZW1lbnQgc2FtcGxlLCBjb250cm9sbGVyIHZlbG9jaXR5LCByZXNvbHZlZCB2ZWxvY2l0eSwgcGhhc2UsIGZhY3RzLCBjYXBzdWxlIGhlaWdodCBhbmQgb3ZlcmxhcCBkZXB0aC4gVGlja0FkdmFuY2VkIHZlcmlmaWVzIHRoZSBtYXRjaGluZyBtb3ZlbWVudC9mYWN0IHB1YmxpY2F0aW9uIGNvdW50LiBSZXRhaW4gZXZlcnkgemVyby9zbG93L2Jsb2NrZWQvZmFpbGluZyB0aWNrOyByZXBvcnQgcGhhc2UgbWluL21heCBzcGVlZHMgYW5kIGV4YWN0IGhvcml6b250YWwgc3RhdGlvbmFyeSBpbnRlcnZhbHMgZGVzY3JpcHRpdmVseS4gU2F2ZSB1bmNoYW5nZWQgc291cmNlL2NvbmZpZy9zY2VuZSBoYXNoZXMsIGFsbCAyNSBtYXJrZXIgc25hcHNob3RzIGFuZCBhY3R1YWwgY29sbGlzaW9uL3JlbmRlciBnZW9tZXRyeS4gRW5jb2RlIHRoZSBvcmlnaW5hbCBuYXRpdmUgY2FwdHVyZSBhcyBleHBsaWNpdGx5IGluY29tcGxldGUgZGlhZ25vc3RpYyBzY29wZTsgb3JkaW5hcnkgc3VzcGVuc2lvbidzIGxpZmVjeWNsZSBjbG9zZSBpcyByZXRhaW5lZCBzZXBhcmF0ZWx5LiBUaGUgcHVibGljIElucHV0UmVjb3JkaW5nUHJlc2VudGVyIGRlY29kZXIgbXVzdCByZWplY3QgdGhlIG9yaWdpbmFsIENvbXBsZXRlPWZhbHNlIGZpbGUgYXMgaW5jb21wbGV0ZS4gQSB0ZXN0LW9ubHkgZm9yZW5zaWMgQmluYXJ5UmVhZGVyIHJlYWRzIGl0cyBvcmlnaW5hbCBoZWFkZXIsIHByb3ZlbmFuY2UsIGZhbHNlIGNvbXBsZXRlbmVzcyBiaXQgYW5kIGV2ZXJ5IHJlY29yZCB3aXRob3V0IG1vZGlmeWluZyBhbnkgYnl0ZXMgb3IgY2FsbGluZyBnYW1lcGxheSBwbGF5YmFjay4gUmUtZW5jb2RpbmcgdGhlIGZ1bGwgb2JzZXJ2ZWQgcmVjb3JkcyB3aXRoIG9yaWdpbmFsIHByb3ZlbmFuY2UgYW5kIENvbXBsZXRlPWZhbHNlIG11c3QgZXF1YWwgdGhlIG5hdGl2ZSBieXRlcy4gUmVwbGF5IHRob3NlIGluZGVwZW5kZW50bHkgcGFyc2VkIHJlY29yZHMgdGhyb3VnaCBQbGF5ZXJDb250cm9sbGVyIGZyb20gdGhlIG9yaWdpbmFsIHNwYXduIHVzaW5nIHJlY29yZGVkIHByb2Jlcy9yZXNvbHV0aW9uczsgdGhpcyBpcyBzb3VyY2Utb25seSByZWNvcmRlZC1yZXNvbHV0aW9uIHJlcGxheSwgbm90IGlucHV0LW9ubHkgZGV0ZXJtaW5pc3RpYyBwaHlzaWNzLgoKQXR0YWNoIHRvIHRoZSBhY3R1YWwgYXV0aG9yZWQgQ2FtZXJhRHJpdmVyIG91dHB1dCBjYW1lcmEncyBlbmRDYW1lcmFSZW5kZXJpbmc7IHNhdmUgZWFjaCBzZWxlY3RlZCByZW5kZXIncyBmcmFtZS90aWNrLCBpbnB1dC9wb3NlLCBjYW1lcmEgcG9zZS9tYXRyaWNlcy9GT1Yvc2NyZWVuIGdlb21ldHJ5IGFuZCBjdXJyZW50IHBoYXNlLiBBc3NvY2lhdGUgb2JzZXJ2YXRpb24gbGFiZWxzIHdpdGggdW5lZGl0ZWQgZGVmZXJyZWQgR2FtZSBWaWV3IFBOR3MgZm9yIGVhY2ggYXV0aG9yZWQgY3VlJ3MgZmlyc3Qgb3JkaW5hcnktc3BlZWQgYXBwcm9hY2gsIGZpcnN0IGFjdHVhbCBpbnRlcmFjdGlvbi9jcm9zc2luZywgYW5kIGRlcGFydHVyZSwgcGx1cyBlYWNoIGdyb3VuZCByb3V0ZSdzIGZpcnN0IG1vdmluZyB2aWV3IGFuZCBlYWNoIHdheXBvaW50IGFycml2YWwuIFRoZSBleHBsaWNpdCAwMjUgbWFwcGluZyBhbmQgb25lLXBoeXNpY2FsLWltYWdlLXBlci1mcmFtZSBydWxlcyBhYm92ZSBhcHBseS4gQSBzcGVlZC1hcHByb2FjaCByZXF1ZXN0IHN0YXJ0cyBhdCBjb250cm9sbGVyIGhvcml6b250YWwgc3BlZWQgPj03LjVtL3MgYW5kIHdpdGhpbiA2bSBvZiB0aGUgY3VlOyByZXBvcnQgYWN0dWFsIHNwZWVkLCBuZXZlciBjYWxsIHRoZSB0aHJlc2hvbGQgaXRzZWxmIGFjY2VwdGFuY2UuIElmIGEgY3VlIGlzIHJlYWNoZWQgc29vbmVyIG9yIG5ldmVyIG1lZXRzIHRoYXQgZGVzY3JpcHRpdmUgcmVxdWVzdCwgcmV0YWluIGEgZmFsbGJhY2sgZmlyc3QgbmVhcmJ5IHZpZXcgcGx1cyBhbiBleHBsaWNpdCBtaXNzaW5nLXNwZWVkLXZpZXcgbGFiZWwuIFJlcXVlc3QgYW5kIGZpcnN0IHN1YnNlcXVlbnQgcmVuZGVyL2ZpbGUgY29tcGxldGlvbiBjYXJyeSBkaXN0aW5jdCBvYnNlcnZhdGlvbnM7IGRlZmVycmVkIHBpeGVscyBhcmUgbm90IGFzc2lnbmVkIHRoZSByZXF1ZXN0J3MgZXhhY3QgdGltZXN0YW1wLiBObyBmb3JjZWQgQ2FtZXJhLlJlbmRlciwgdGFyZ2V0LXRleHR1cmUgY2hhbmdlcywgb2ZmLXNjcmVlbiBpbWFnZSBzeW50aGVzaXMgb3IgY3JvcHBpbmcuIFNhdmUgcGVuZGluZy9taXNzaW5nIHJlcXVlc3RzIGFuZCB0aGUgb3JpZ2luYWwgaW1hZ2VzLiBQTkcgZHJhaW4gbWF5IHdhaXQgdXAgdG8gMiB3YWxsIHNlY29uZHMgYWZ0ZXIgc2ltdWxhdGlvbiBzdXNwZW5zaW9uOyBubyBhZGRpdGlvbmFsIGdhbWVwbGF5IHRpY2tzIGFyZSBhZGRlZC4KCk5Vbml0IHN1Y2Nlc3MgbWVhbnMgY29sbGVjdGlvbiBpbnRlZ3JpdHkgb25seS4gRWFjaCByb3V0ZSBoYXMgaW5kZXBlbmRlbnQgcGh5c2ljYWwtcm91dGUgdmVyZGljdCAob3JkZXJlZCB3YXlwb2ludHMvbWFya2VycywgcGVybWl0dGVkIGZhY3RzLCBmbG9vci9jYXBzdWxlIGJlaGF2aW9yLCBmaW5pc2gpLCBhbmQgaW1hZ2UgY29tcGxldGVuZXNzIHJlcG9ydC4gUm9vdCBtdXN0IGluc3BlY3Qgb3JpZ2luYWwgcGl4ZWxzIGFuZCBhY3R1YWwgc3BlZWQvcGhhc2UgdHJhY2VzIHRvIGFzc2VzcyByZWFkYWJsZSBhZmZvcmRhbmNlczsgZnJ1c3R1bSBtZXRhZGF0YSBvciBzY3JlZW5zaG90IGV4aXN0ZW5jZSBhbG9uZSBuZXZlciBtYXJrcyByZWFkYWJpbGl0eSBhY2NlcHRlZC4gQSBncmVlbiB0ZXN0IGNhbm5vdCB3YWl2ZSBhIHBoeXNpY2FsIG9yIGN1ZSBmYWlsdXJlLgo=";
        private const float Dt = 1f / 60f;
        private const int MaximumTicks = 2400;

        [UnityTest, Explicit("Frozen authored-line witnesses; NUnit verifies collection integrity only. Read physical and visual evidence.")]
        public IEnumerator DeclaredAuthoredLinesRetainContinuousMotionAndActualCameraEvidence()
        {
            yield return new EnterPlayMode();
            yield return Collect();
        }
        [UnityTearDown] public IEnumerator RestoreEditor() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void RenderLabelsAtOneRenderShareOnePhysicalImageAndRetainEachTrigger()
        {
            var report = new RenderObserverReport { folder = "metadata-only" };
            var admission = new RenderImageAdmission(report);
            admission.Add(new RenderLabelRequest { label = "approach", triggerTick = 10, triggerObservedTick = 10, triggerFrame = 20, triggerRealtimeSeconds = 1.0 });
            admission.Add(new RenderLabelRequest { label = "interaction", triggerTick = 11, triggerObservedTick = 11, triggerFrame = 20, triggerRealtimeSeconds = 1.01 });
            admission.Add(new RenderLabelRequest { label = "fact", triggerTick = 12, triggerObservedTick = 12, triggerFrame = 20, triggerRealtimeSeconds = 1.02 });
            RenderImageRequest batch = admission.Admit(new RenderFrameObservation { sequence = 1, frame = 20, tick = 12, realtimeSeconds = 1.03 });
            Assert.That(report.requests.Count, Is.EqualTo(1));
            Assert.That(batch.labelSequences, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(batch.labels, Is.EqualTo(new[] { "approach", "interaction", "fact" }));
            Assert.That(report.labels.Select(item => item.triggerTick), Is.EqualTo(new long[] { 10, 11, 12 }));
            Assert.That(report.labels.Select(item => item.requestTickLag), Is.EqualTo(new long[] { 2, 1, 0 }));
            Assert.That(report.labels.All(item => item.imageSequence == batch.sequence), Is.True);
            Assert.That(admission.MissingPhysicalImages, Is.EqualTo(1));
            Assert.That(admission.UnassignedUniqueLabels, Is.Zero);
            Assert.That(admission.UncoveredUniqueLabels, Is.EqualTo(3));
            Assert.That(batch.captureCallAttempted, Is.False, "Metadata admission never calls an image API.");
        }

        [Test]
        public void RenderLabelAfterCaptureInSameFrameWaitsWithoutBackdatingAssociation()
        {
            var report = new RenderObserverReport { folder = "metadata-only" };
            var admission = new RenderImageAdmission(report);
            admission.Add(new RenderLabelRequest { label = "first", triggerTick = 10, triggerObservedTick = 10, triggerFrame = 20 });
            RenderImageRequest first = admission.Admit(new RenderFrameObservation { sequence = 1, frame = 20, tick = 10 });
            admission.Add(new RenderLabelRequest { label = "later", triggerTick = 11, triggerObservedTick = 11, triggerFrame = 20, triggerLastRenderSequence = 1 });
            Assert.That(admission.Admit(new RenderFrameObservation { sequence = 2, frame = 20, tick = 11 }), Is.Null);
            Assert.That(report.requests.Count, Is.EqualTo(1));
            Assert.That(report.labels[1].imageSequence, Is.Zero);
            Assert.That(report.sameFrameDeferrals.Single().labelSequences, Is.EqualTo(new[] { 2 }));
            Assert.That(admission.Missing, Is.EqualTo(2), "One missing image plus one queued unique label.");
            RenderImageRequest next = admission.Admit(new RenderFrameObservation { sequence = 3, frame = 21, tick = 12 });
            Assert.That(next.sequence, Is.EqualTo(2));
            Assert.That(first.labelSequences, Is.EqualTo(new[] { 1 }), "No retroactive association with an earlier image.");
            Assert.That(next.labelSequences, Is.EqualTo(new[] { 2 }));
            Assert.That(report.labels[1].requestFrameLag, Is.EqualTo(1));
            Assert.That(report.labels[1].requestTickLag, Is.EqualTo(1));
            Assert.That(report.labels[1].requestRenderSequenceLag, Is.EqualTo(2));
            Assert.That(first.path, Is.Not.EqualTo(next.path));
        }

        [Test]
        public void RenderDuplicateLabelIsRetainedWithoutNewPhysicalRequestOrMissingUnit()
        {
            var report = new RenderObserverReport { folder = "metadata-only" };
            var admission = new RenderImageAdmission(report);
            admission.Add(new RenderLabelRequest { label = "same", triggerFrame = 20, triggerTick = 10 });
            RenderImageRequest batch = admission.Admit(new RenderFrameObservation { sequence = 1, frame = 20, tick = 10 });
            batch.fileComplete = true;
            admission.Add(new RenderLabelRequest { label = "same", triggerFrame = 21, triggerTick = 11 });
            Assert.That(admission.Admit(new RenderFrameObservation { sequence = 2, frame = 21, tick = 11 }), Is.Null);
            Assert.That(report.labels.Count, Is.EqualTo(2));
            Assert.That(report.labels[1].duplicateOfLabel, Is.EqualTo(1));
            Assert.That(report.labels[1].triggerTick, Is.EqualTo(11));
            Assert.That(report.labels[1].imageSequence, Is.Zero, "Duplicate references its original label record, not a later image.");
            Assert.That(report.requests.Count, Is.EqualTo(1));
            Assert.That(admission.RequestedLabels, Is.EqualTo(new[] { "same" }));
            Assert.That(admission.Missing, Is.Zero);
            Assert.That(admission.Pending, Is.False);
        }

        [Test]
        public void RenderFailedPhysicalImageRemainsMissingWithoutImplicitRetry()
        {
            var report = new RenderObserverReport { folder = "metadata-only" };
            var admission = new RenderImageAdmission(report);
            admission.Add(new RenderLabelRequest { label = "a", triggerFrame = 20 });
            admission.Add(new RenderLabelRequest { label = "b", triggerFrame = 20 });
            RenderImageRequest batch = admission.Admit(new RenderFrameObservation { sequence = 1, frame = 20 });
            batch.requestRejected = true; batch.rejectionReason = "Recorded synthetic request failure for metadata test.";
            Assert.That(admission.Pending, Is.False);
            Assert.That(admission.Missing, Is.EqualTo(1));
            Assert.That(admission.UncoveredUniqueLabels, Is.EqualTo(2));
            Assert.That(report.errors, Is.Empty, "Individual image failure is a visual gap.");
            Assert.That(admission.Admit(new RenderFrameObservation { sequence = 2, frame = 21 }), Is.Null);
            admission.Add(new RenderLabelRequest { label = "c", triggerFrame = 21 });
            Assert.That(admission.Pending, Is.True);
            Assert.That(admission.Missing, Is.EqualTo(2));
            RenderImageRequest second = admission.Admit(new RenderFrameObservation { sequence = 3, frame = 21 });
            Assert.That(second.labels, Is.EqualTo(new[] { "c" }));
            second.fileError = "Recorded synthetic PNG failure for metadata test.";
            Assert.That(admission.Pending, Is.False);
            Assert.That(admission.MissingPhysicalImages, Is.EqualTo(2));
            Assert.That(admission.UncoveredUniqueLabels, Is.EqualTo(3));
        }

        [Test]
        public void RenderWithoutQueuedLabelsDoesNotConsumeThatFramesCaptureBudget()
        {
            var report = new RenderObserverReport { folder = "metadata-only" };
            var admission = new RenderImageAdmission(report);
            Assert.That(admission.Admit(new RenderFrameObservation { sequence = 1, frame = 20 }), Is.Null);
            admission.Add(new RenderLabelRequest { label = "arrived-after-first-render", triggerFrame = 20, triggerLastRenderSequence = 1 });
            Assert.That(admission.Admit(new RenderFrameObservation { sequence = 2, frame = 20 }), Is.Not.Null);
            Assert.That(report.requests.Count, Is.EqualTo(1));
            admission.Add(new RenderLabelRequest { label = "arrived-after-image", triggerFrame = 20, triggerLastRenderSequence = 2 });
            Assert.That(admission.Admit(new RenderFrameObservation { sequence = 3, frame = 20 }), Is.Null);
            Assert.That(admission.UnassignedUniqueLabels, Is.EqualTo(1));
        }

        [Test]
        public void RenderFrameBudgetIsSharedAcrossRouteHandoffsWithoutSharingLabelIdentity()
        {
            var budget = new HashSet<int>();
            var firstReport = new RenderObserverReport { folder = "route-one-metadata-only" };
            var secondReport = new RenderObserverReport { folder = "route-two-metadata-only" };
            var first = new RenderImageAdmission(firstReport, budget);
            var second = new RenderImageAdmission(secondReport, budget);
            first.Add(new RenderLabelRequest { label = "departure", triggerFrame = 20, triggerTick = 10 });
            RenderImageRequest original = first.Admit(new RenderFrameObservation { sequence = 1, frame = 20, tick = 10, phase = "route-one-departure" });
            original.requestRejected = true;
            second.Add(new RenderLabelRequest { label = "departure", triggerFrame = 20, triggerTick = 1, triggerPhase = "route-two-start" });
            var deferred = new RenderFrameObservation { sequence = 1, frame = 20, tick = 1, phase = "route-two-start" };
            Assert.That(second.Admit(deferred), Is.Null, "An attempted/rejected first-route image still reserves the frame.");
            Assert.That(secondReport.sameFrameDeferrals.Single().observation, Is.SameAs(deferred));
            Assert.That(secondReport.labels.Single().imageSequence, Is.Zero);
            RenderImageRequest next = second.Admit(new RenderFrameObservation { sequence = 2, frame = 21, tick = 2, phase = "route-two-next" });
            Assert.That(next, Is.Not.Null);
            Assert.That(secondReport.labels.Single().duplicateOfLabel, Is.Zero, "Labels are unique within each route, not across the cohort.");
            Assert.That(secondReport.labels.Single().requestFrameLag, Is.EqualTo(1));
            Assert.That(secondReport.labels.Single().triggerPhase, Is.EqualTo("route-two-start"));
            Assert.That(next.request.phase, Is.EqualTo("route-two-next"));
            Assert.That(original.request.phase, Is.EqualTo("route-one-departure"));
            Assert.That(firstReport.requests.Count + secondReport.requests.Count, Is.EqualTo(2));
        }

        private enum LegKind { Move, Vault, Gate, Rebound, Drop }
        private sealed class Leg
        {
            public readonly string Name;
            public readonly LegKind Kind;
            public readonly Vector3 Target, Direction;
            public readonly int Marker;
            public Leg(string name, LegKind kind, Vector3 target, Vector3 direction = default, int marker = 0)
            { Name = name; Kind = kind; Target = target; Direction = direction; Marker = marker; }
        }
        private sealed class Route
        {
            public readonly string Name;
            public readonly Vector3 Spawn;
            public readonly Leg[] Legs;
            public Route(string name, float x, float z, params Leg[] legs)
            { Name = name; Spawn = new Vector3(x, 0.05f, z); Legs = legs; }
        }
        private static Leg Move(string name, float x, float z, float y = 0f) => new Leg(name, LegKind.Move, new Vector3(x, y, z));
        private static Leg Vault(int id, float x, float z, Vector3 direction) => new Leg("vault-" + id, LegKind.Vault, new Vector3(x, 0f, z), direction, id);
        private static Leg Gate(int id, Vector3 direction) => new Leg("slide-" + id, LegKind.Gate, default, direction, id);
        private static Route[] Routes() => new[]
        {
            new Route("low-lower-east", -21f, -1f, Move("shared-101", -10f, -1f), Move("lower-approach", -9f, -4f), Move("lower-102", 16f, -4f)),
            new Route("lower-low-west", 16f, -4f, Move("lower-102", -9f, -4f), Move("shared-approach", -10f, -1f), Move("shared-101", -21f, -1f)),
            new Route("upper-east", 0f, 4f, Move("upper-103", 16f, 4f)),
            new Route("upper-west", 16f, 4f, Move("upper-103", 0f, 4f)),
            new Route("microloop-clockwise", -9f, -2f, Move("south", -1f, -2f), Move("east", -1f, 8f), Move("north", -9f, 8f), Move("west", -9f, -2f)),
            new Route("microloop-counterclockwise", -9f, -2f, Move("west", -9f, 8f), Move("north", -1f, 8f), Move("east", -1f, -2f), Move("south", -9f, -2f)),
            new Route("vault201-south", -4f, -9f, Vault(201, -4f, -4.5f, Vector3.forward), Move("vault-201-departure", -4f, -2.5f)),
            new Route("rebound202-west", -11f, 3f, new Leg("rebound-202-west", LegKind.Rebound, new Vector3(-10.5f, 0f, 3f), Vector3.right, 202)),
            new Route("rebound202-east", 1f, 3f, new Leg("rebound-202-east", LegKind.Rebound, new Vector3(0.5f, 0f, 3f), Vector3.left, 202)),
            new Route("clutter-east", -21f, 5f, Move("west-join", -21f, 15f), Vault(210, -8.4f, 15f, Vector3.right), Gate(220, Vector3.right),
                Vault(211, 5.6f, 15f, Vector3.right), Gate(221, Vector3.right), Vault(212, 19.6f, 15f, Vector3.right),
                Move("east-corner", 27f, 15f), Move("east-join", 27f, 8.5f)),
            new Route("clutter-west", 27f, 8.5f, Move("east-join", 27f, 15f), Vault(212, 16.4f, 15f, Vector3.left), Gate(221, Vector3.left),
                Vault(211, 2.4f, 15f, Vector3.left), Gate(220, Vector3.left), Vault(210, -11.6f, 15f, Vector3.left),
                Move("west-corner", -21f, 15f), Move("west-join", -21f, 5f)),
            new Route("vertical-drop", 13f, -6f, Move("ramp-to-deck", 26f, -6f, 4f), Move("upper-deck-turn", 26f, 4f, 4f),
                Move("bridge-entry", 22f, 4f, 4f), new Leg("bridge-lip-natural-drop", LegKind.Drop, default, Vector3.left, 106))
        };

        private static IEnumerator Collect()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "AgentValidation", "PLAN-004", "design-speed-025",
                DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(folder);
            byte[] protocol = Convert.FromBase64String(ProtocolBase64);
            Assert.That(Hash(protocol), Is.EqualTo(ProtocolSha256));
            File.WriteAllBytes(Path.Combine(folder, "PROTOCOL.md"), protocol); File.Copy(Fixture, Path.Combine(folder, "fixture-source.cs"));
            Route[] routes = Routes();
            var report = new CohortReport { startedUtc = DateTime.UtcNow.ToString("O"), protocolSha256 = ProtocolSha256,
                fixtureSha256 = Hash(File.ReadAllBytes(Fixture)), sourceRevision = BuildHash("Assets/Scripts", "*.cs"),
                configHash = BuildHash("Assets/Resources/ScriptableObjects", "*.asset"), sceneDependencyHash = AssetDatabase.GetAssetDependencyHash(Arena).ToString(),
                routes = routes.Select((r, i) => new RouteReport { number = i + 1, name = r.Name, spawn = r.Spawn,
                    plannedLegs = r.Legs.Select(l => l.Name).ToArray() }).ToArray() };
            Write(Path.Combine(folder, "pre-run-declaration.json"), report);
            File.WriteAllText(Path.Combine(folder, "frozen-routes.json"), Json(routes.Select(r => new { name = r.Name, spawn = r.Spawn,
                legs = r.Legs.Select(l => new { name = l.Name, kind = l.Kind, target = l.Target, direction = l.Direction, marker = l.Marker }).ToArray() }).ToArray()));
            bool background = Application.runInBackground;
            bool collectionLoopCompleted = false;
            using (var gate = new CaptureGateTrace("TagArenaDesignSpeed024"))
            {
                try
                {
                    Application.runInBackground = true;
                    yield return gate.AdmitStableGameViewFocus();
                    for (int i = 0; i < routes.Length; i++)
                    {
                        using (var trial = new Trial(routes[i], report.routes[i], report, folder))
                        {
                            SceneManager.sceneLoaded += trial.Arrange;
                            try
                            {
                                var load = SceneManager.LoadSceneAsync(Arena, LoadSceneMode.Single);
                                if (load == null) trial.Fail("Scene load returned null.");
                                double deadline = Time.realtimeSinceStartupAsDouble + 20d;
                                while (!trial.Ready && !trial.Failed && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                                if (!trial.Ready || load == null || !load.isDone) trial.Fail("20-second scene readiness deadline.");
                                deadline = Time.realtimeSinceStartupAsDouble + 90d;
                                while (!trial.Done && !trial.Failed && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                                if (!trial.Done && !trial.Failed) trial.Fail("90-second native collection deadline.");
                                trial.Close(); deadline = Time.realtimeSinceStartupAsDouble + 2d;
                                while (trial.PendingImages && Time.realtimeSinceStartupAsDouble < deadline) { trial.PollImages(); yield return null; }
                                trial.Analyze();
                            }
                            finally { SceneManager.sceneLoaded -= trial.Arrange; }
                        }
                        try { Write(Path.Combine(folder, "cohort-report.json"), report); }
                        catch (Exception error) { report.finalizationFailure = "Cohort progress report: " + error; break; }
                        if (!report.routes[i].collectionIntegrityPassed) break;
                    }
                    collectionLoopCompleted = true;
                }
                finally
                {
                    try
                    {
                        report.finishedUtc = DateTime.UtcNow.ToString("O");
                        if (!collectionLoopCompleted) report.finalizationFailure += "Collection exited before normal loop completion; inspect the original NUnit failure. ";
                        try { report.gateDiagnostics = gate.Describe(); }
                        catch (Exception error) { report.finalizationFailure += "Capture gate finalization: " + error; }
                        try { gate.Dispose(); }
                        catch (Exception error) { report.finalizationFailure += "Capture gate disposal: " + error; }
                        report.collectionIntegrityPassed = collectionLoopCompleted && report.finalizationFailure.Length == 0 && report.routes.All(r => r.collectionIntegrityPassed);
                        report.physicalRoutesPassed = report.routes.All(r => r.physicalRoutePassed);
                        try { Write(Path.Combine(folder, "cohort-report.json"), report); }
                        catch (Exception error)
                        {
                            report.collectionIntegrityPassed = false;
                            report.finalizationFailure += "Cohort report finalization: " + error;
                            TestContext.WriteLine(report.finalizationFailure);
                        }
                    }
                    finally { Application.runInBackground = background; }
                }
            }
            TestContext.WriteLine("Authored-line 025: " + folder + "; collection=" + report.collectionIntegrityPassed + "; physical=" + report.physicalRoutesPassed);
            Assert.That(report.collectionIntegrityPassed, Is.True, "Collection failed; reserved and partial routes retained at " + folder);
        }

        [Serializable] private sealed class CohortReport
        {
            [NonSerialized] public readonly HashSet<int> captureFrames = new HashSet<int>();
            public string startedUtc, finishedUtc, protocolSha256, fixtureSha256, sourceRevision, configHash, sceneDependencyHash, gateDiagnostics;
            public string finalizationFailure = "";
            public bool collectionIntegrityPassed, physicalRoutesPassed;
            public string visualAcceptance = "Inspect original pixels, request/subsequent-render timing and actual speed/probe rows; not verified by NUnit.";
            public string scope = "Twelve free-Player authored lines. No human, hardware, chase-population or new speed-threshold claim.";
            public RouteReport[] routes;
        }
        [Serializable] private sealed class RouteReport
        {
            public int number, committedTicks, physicalFrames, movementEvents, traversalEvents, completedLegs, replayRecords;
            public string name, status = "reserved-not-started", failure = "", termination = "", sessionId, nativePath, nativeSha256,
                lifecyclePath, lifecycleSha256, tickRowsSha256, geometrySha256, startedUtc, finishedUtc;
            public Vector3 spawn;
            public string[] plannedLegs;
            public bool collectionIntegrityPassed, physicalRoutePassed, replayPassed, renderIntegrityPassed;
            public int missingCaptureUnits;
            public List<string> physicalIssues = new List<string>(), transitions = new List<string>(), missingSpeedViews = new List<string>();
            public List<string> missingCueLabels = new List<string>(), renderErrors = new List<string>();
            public List<CueBinding> cueBindings = new List<CueBinding>();
            public List<CueObservation> cueObservations = new List<CueObservation>();
            public List<LegStats> legs = new List<LegStats>();
            public List<Stationary> stationary = new List<Stationary>();
            public string binaryScope = "Native Complete=false. Public playback rejects it; forensic reader inspects unchanged bytes and source replay uses recorded resolutions.";
        }
        [Serializable] private sealed class CueBindingReport { public List<CueBinding> bindings; }
        [Serializable] private sealed class CueBinding
        {
            public string leg, kind, source, sourceObject, crossingMeaning;
            public int marker;
            public Vector3 position, boundsCenter, boundsSize, travelDirection;
        }
        [Serializable] private sealed class CueObservation
        {
            public string label, leg, kind, source, sourceObject, relation;
            public int marker;
            public long tick;
            public Vector3 cuePosition, playerPosition;
            public float horizontalDistance, signedPlaneDistance, resolvedSpeed, controllerSpeed;
        }
        [Serializable] private sealed class LegStats
        {
            public string name; public long firstTick, lastTick; public int ticks, zeroHorizontalTicks;
            public float minimumResolvedSpeed = float.MaxValue, maximumResolvedSpeed, minimumDecisionSpeed = float.MaxValue, maximumDecisionSpeed;
        }
        [Serializable] private sealed class Stationary { public string leg, state; public long firstTick, lastTick; public float seconds; }
        private sealed class Sample
        {
            public InputProbeRecord Record; public PlayerMovementSample Movement; public Vector3 DecisionVelocity; public PlayerTraversalFact[] Facts; public string Phase;
        }

        private sealed class Trial : IDisposable
        {
            private readonly Route route;
            private readonly RouteReport report;
            private readonly CohortReport cohort;
            private readonly string folder;
            private readonly List<Sample> samples = new List<Sample>();
            private readonly List<PlayerTraversalFact> publishedFacts = new List<PlayerTraversalFact>();
            private readonly Dictionary<int, LevelMarker> markers = new Dictionary<int, LevelMarker>();
            private readonly Dictionary<int, string> markerStates = new Dictionary<int, string>();
            private readonly List<GameObject> disabledHunters = new List<GameObject>();
            private readonly StreamWriter rows;
            private TagArenaSceneRoot root;
            private Vector3 originalSpawn;
            private int originalSeed;
            private bool restoreRoot, restoreDevices, closed, analyzed, requested, secondRequested, underGate, standingBlocked, reboundObserved, dropAir;
            private bool approachImage, fallbackImage, interactionImage, firstMovingImage;
            private RunSessionManager run, captureRun;
            private InputManager input;
            private PlayerManager player;
            private PlayerProfile profile;
            private PlayerMoverDriverConfig mover;
            private CapsuleCollider capsule;
            private Collider[] geometry;
            private string profileSnapshot, moverSnapshot;
            private InputActionMap gameplay;
            private ReadOnlyArray<InputDevice>? previousDevices;
            private RunCaptureMetadata metadata;
            private InputFrame supplied;
            private InputButtons previousHeld;
            private int captures, legIndex, legTicks, progressTick;
            private Vector3 progressPosition;
            private LegStats stats;
            private RenderObserver observer;
            private CueBinding currentCue;
            private float previousCueSigned;
            public bool Ready { get; private set; }
            public bool Done { get; private set; }
            public bool Failed => report.failure.Length != 0;
            public bool PendingImages => observer != null && observer.Pending;
            private Leg Current => route.Legs[Math.Min(legIndex, route.Legs.Length - 1)];

            public Trial(Route route, RouteReport report, CohortReport cohort, string parent)
            {
                this.route = route; this.report = report; this.cohort = cohort;
                folder = Path.Combine(parent, report.number.ToString("00") + "-" + route.Name); Directory.CreateDirectory(folder);
                rows = new StreamWriter(Path.Combine(folder, "ticks.jsonl"), false, new UTF8Encoding(false)) { AutoFlush = true };
                report.startedUtc = DateTime.UtcNow.ToString("O"); report.status = "loading"; progressPosition = route.Spawn;
            }
            public void Arrange(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != Arena) return;
                try
                {
                    VerifyFiles(); root = One<TagArenaSceneRoot>();
                    originalSpawn = (Vector3)Field(root, "_spawnPosition").GetValue(root); originalSeed = (int)Field(root, "_seed").GetValue(root); restoreRoot = true;
                    Field(root, "_spawnPosition").SetValue(root, route.Spawn); Field(root, "_seed").SetValue(root, 1);
                    profile = (PlayerProfile)new SerializedObject(root).FindProperty("_playerProfile").objectReferenceValue;
                    profileSnapshot = EditorJsonUtility.ToJson(profile);
                    var level = One<LevelManager>();
                    foreach (var marker in level.GetComponentsInChildren<LevelMarker>(true))
                    { markers.Add(marker.SurfaceId, marker); markerStates.Add(marker.SurfaceId, EditorJsonUtility.ToJson(marker)); }
                    Assert.That(markers.Count, Is.EqualTo(25));
                    geometry = level.GetComponentsInChildren<Collider>(true).Where(c => c.enabled && !c.isTrigger).ToArray();
                    string snapshot = Json(new { markers = markers.OrderBy(p => p.Key).Select(p => new { id = p.Key, record = p.Value.Capture(),
                            serialized = markerStates[p.Key], position = p.Value.transform.position }).ToArray(),
                        colliders = geometry.Select(c => new { name = c.name, center = c.bounds.center, size = c.bounds.size,
                            position = c.transform.position, rotation = c.transform.eulerAngles, scale = c.transform.lossyScale, layer = c.gameObject.layer }).ToArray(),
                        renderers = level.GetComponentsInChildren<Renderer>(true).Select(r => new { name = r.name, enabled = r.enabled,
                            center = r.bounds.center, size = r.bounds.size, materials = r.sharedMaterials.Select(m => m == null ? "missing" : AssetDatabase.GetAssetPath(m)).ToArray() }).ToArray() });
                    File.WriteAllText(Path.Combine(folder, "actual-geometry.json"), snapshot); report.geometrySha256 = Hash(File.ReadAllBytes(Path.Combine(folder, "actual-geometry.json")));
                    captureRun = RunSessionManager.Instance != null ? RunSessionManager.Instance : (RunSessionManager)Field(root, "_run").GetValue(root);
                    captureRun.CaptureStarted += CaptureStarted; TagArenaSceneRoot.SceneReady += SceneReady;
                }
                catch (Exception error) { Fail("Arrangement: " + error); }
            }
            private void CaptureStarted(RunCaptureMetadata value)
            {
                captures++; if (captures != 1) { Fail("Repeated CaptureStarted."); return; }
                metadata = value; report.sessionId = value.SessionId;
                File.WriteAllText(Path.Combine(folder, "capture-metadata.json"), Json(value));
            }
            private void SceneReady(SceneKey scene)
            {
                if (scene != SceneKey.TagArena) return;
                try
                {
                    run = RunSessionManager.Instance; input = InputManager.Instance; player = One<PlayerManager>();
                    Assert.That(run.Tick, Is.Zero); Assert.That(captures, Is.EqualTo(1)); Assert.That(metadata.StartTick, Is.Zero); Assert.That(metadata.Seed, Is.EqualTo(1));
                    Assert.That(metadata.FixedDeltaTime, Is.EqualTo(Dt)); Assert.That(Time.fixedDeltaTime, Is.EqualTo(Dt)); Assert.That(Time.timeScale, Is.EqualTo(1f));
                    Assert.That(metadata.SourceRevision, Is.EqualTo(cohort.sourceRevision)); Assert.That(metadata.ConfigSnapshotHash, Is.EqualTo(cohort.configHash));
                    Assert.That(metadata.SessionId, Is.Not.Null.And.Not.Empty); Assert.That(metadata.RandomConsumptionOrder, Is.Not.Null.And.Not.Empty);
                    Assert.That(player.ReadOnlyState.Position, Is.EqualTo(route.Spawn)); Assert.That(player.ReadOnlyState.HeadingDegrees, Is.EqualTo(90f));
                    Assert.That(player.ReadOnlyState.Velocity, Is.EqualTo(Vector3.zero)); Assert.That(PlayerRegistry.Items.Count, Is.EqualTo(1));
                    foreach (var hunter in UnityEngine.Object.FindObjectsByType<HunterManager>(FindObjectsSortMode.None)) { disabledHunters.Add(hunter.gameObject); hunter.gameObject.SetActive(false); }
                    Assert.That(HunterRegistry.Items.Count, Is.Zero);
                    capsule = player.GetComponent<CapsuleCollider>();
                    mover = (PlayerMoverDriverConfig)new SerializedObject(player.GetComponent<PlayerDriver>()).FindProperty("_config").objectReferenceValue;
                    moverSnapshot = EditorJsonUtility.ToJson(mover);
                    Assert.That(profile.SprintSpeed, Is.EqualTo(8f)); Assert.That(profile.MaxDesignSpeed, Is.EqualTo(14f));
                    File.WriteAllText(Path.Combine(folder, "profile.json"), profileSnapshot); File.WriteAllText(Path.Combine(folder, "mover.json"), moverSnapshot);
                    gameplay = (InputActionMap)Field(input.GetComponent<PlayerInputDriver>(), "_actions").GetValue(input.GetComponent<PlayerInputDriver>());
                    previousDevices = gameplay.devices.HasValue ? new ReadOnlyArray<InputDevice>(gameplay.devices.Value.ToArray()) : (ReadOnlyArray<InputDevice>?)null;
                    restoreDevices = true; gameplay.devices = Array.Empty<InputDevice>();
                    foreach (InputAction action in gameplay.actions) Assert.That(action.controls.Count, Is.Zero);
                    Assert.That(input.SetSource(InputSource.Live), Is.True);
                    input.FramePublished += Supply; run.PlayerProbeRecorded += Record; run.PlayerMovementPublished += Movement;
                    run.PlayerTraversalPublished += Traversal; run.TickAdvanced += Committed;
                    observer = new RenderObserver(run, player, folder, () => Current.Name, cohort.captureFrames);
                    report.cueBindings.AddRange(route.Legs.Select(BindCue));
                    Write(Path.Combine(folder, "cue-bindings.json"), new CueBindingReport { bindings = report.cueBindings });
                    BeginLeg(); Ready = true; report.status = "collecting";
                }
                catch (Exception error) { Fail("Readiness: " + error); }
            }
            private void BeginLeg()
            {
                requested = secondRequested = underGate = standingBlocked = reboundObserved = dropAir = false;
                approachImage = fallbackImage = interactionImage = false; legTicks = 0;
                progressPosition = player.ReadOnlyState.Position; progressTick = samples.Count;
                stats = new LegStats { name = Current.Name, firstTick = samples.Count + 1 }; report.legs.Add(stats);
                currentCue = report.cueBindings[legIndex];
                previousCueSigned = Vector3.Dot(Flat(player.ReadOnlyState.Position - currentCue.position), currentCue.travelDirection);
            }
            private void Supply(InputFrame physical)
            {
                if (closed || Done || Failed) { run.ReceiveInput(default); return; }
                try
                {
                    Assert.That(physical, Is.EqualTo(default(InputFrame))); report.physicalFrames++;
                    var state = player.ReadOnlyState; var probe = player.LastProbeRecord.Probe; Leg leg = Current;
                    Vector3 desired = leg.Kind == LegKind.Move ? Flat(leg.Target - state.Position).normalized : leg.Direction;
                    if (leg.Kind == LegKind.Rebound && reboundObserved) desired = -desired;
                    if (desired.sqrMagnitude < 0.99f) desired = Quaternion.Euler(0f, state.HeadingDegrees, 0f) * Vector3.forward;
                    InputButtons held = InputButtons.Sprint;
                    if (leg.Kind == LegKind.Vault && !requested && probe.VaultCandidate && probe.VaultClearance > 0f && !probe.StandingBlocked &&
                        Vector3.Distance(probe.VaultTarget, leg.Target) < 0.01f)
                    { requested = true; held = InputButtons.Sprint | InputButtons.Jump; }
                    if (leg.Kind == LegKind.Gate)
                    {
                        Bounds b = markers[leg.Marker].GetComponent<Collider>().bounds;
                        float nearDistance = leg.Direction.x > 0f ? b.min.x - state.Position.x : state.Position.x - b.max.x;
                        if (!requested && nearDistance <= 2f && player.LastProbeRecord.Resolution.Grounded && Speed(state.Velocity) >= profile.SlideMinimumSpeed - 0.00001f) requested = true;
                        if (requested) held = InputButtons.Sprint | InputButtons.Crouch;
                    }
                    if (leg.Kind == LegKind.Rebound)
                    {
                        Bounds b = markers[202].GetComponent<Collider>().bounds;
                        float faceDistance = leg.Direction.x > 0f ? b.min.x - state.Position.x : state.Position.x - b.max.x;
                        if (!requested && faceDistance <= 2.2f && player.LastProbeRecord.Resolution.Grounded)
                        { requested = true; held = InputButtons.Sprint | InputButtons.Jump; }
                        else if (requested && !secondRequested && (previousHeld & InputButtons.Jump) == 0 && state.MovementState == MovementState.Air &&
                            probe.WallDetected && probe.WallId == 202 && probe.WallDistance <= profile.ReboundDistance && probe.WallAngleDegrees <= profile.ReboundAngle)
                        { secondRequested = true; held = InputButtons.Sprint | InputButtons.Jump; }
                    }
                    float bearing = Mathf.Atan2(desired.x, desired.z) * Mathf.Rad2Deg;
                    float yaw = Mathf.Clamp(Mathf.DeltaAngle(state.HeadingDegrees, bearing), -15f, 15f);
                    Vector3 forward = Quaternion.Euler(0f, state.HeadingDegrees + yaw, 0f) * Vector3.forward;
                    Vector3 right = Vector3.Cross(Vector3.up, forward);
                    supplied = new InputFrame(new Vector2(Vector3.Dot(desired, right), Vector3.Dot(desired, forward)), new Vector2(yaw, 0f),
                        held, held & ~previousHeld, previousHeld & ~held);
                    previousHeld = held; run.ReceiveInput(supplied);
                }
                catch (Exception error) { Fail("Input: " + error); run.ReceiveInput(default); }
            }
            private void Record(InputProbeRecord record)
            {
                if (closed) return;
                Leg leg = Current;
                var sample = new Sample { Record = record, Movement = player.LastMovementSample, DecisionVelocity = player.ReadOnlyState.Velocity,
                    Facts = player.LastTraversalFacts.ToArray(), Phase = leg.Name };
                samples.Add(sample); report.committedTicks = samples.Count; legTicks++;
                float penetration = 0f;
                try
                {
                    foreach (var collider in geometry)
                        if (collider.bounds.Intersects(capsule.bounds) && Physics.ComputePenetration(capsule, capsule.transform.position, capsule.transform.rotation,
                            collider, collider.transform.position, collider.transform.rotation, out _, out float depth)) penetration = Mathf.Max(penetration, depth);
                    rows.WriteLine("{\"record\":" + Json(record) + ",\"suppliedInput\":" + Json(supplied) + ",\"movement\":" + Json(sample.Movement) +
                        ",\"decisionVelocity\":" + Json(sample.DecisionVelocity) + ",\"facts\":" + Json(sample.Facts) + ",\"phase\":" + Json(sample.Phase) +
                        ",\"capsuleHeight\":" + Json(capsule.height) + ",\"penetration\":" + Json(penetration) + "}");
                    Assert.That(record.Tick, Is.EqualTo(samples.Count)); Assert.That(record.Input, Is.EqualTo(supplied)); Assert.That(record.DeltaTime, Is.EqualTo(Dt));
                    Assert.That(report.physicalFrames, Is.EqualTo(samples.Count)); Assert.That(record.Resolution.Present, Is.True);
                    Assert.That(record.Input.Move.sqrMagnitude, Is.GreaterThan(0.99f)); Assert.That(record.Input.Held & InputButtons.Sprint, Is.EqualTo(InputButtons.Sprint));
                    Assert.That(Finite(record.Resolution.Position.sqrMagnitude) && Finite(record.Resolution.Velocity.sqrMagnitude) && Finite(sample.DecisionVelocity.sqrMagnitude), Is.True);
                    Assert.That(sample.Movement.Tick, Is.EqualTo(record.Tick)); Assert.That(sample.Movement.Id, Is.EqualTo(player.Id)); Assert.That(HunterRegistry.Items.Count, Is.Zero);
                    float speed = Speed(record.Resolution.Velocity), decision = Speed(sample.DecisionVelocity);
                    stats.lastTick = record.Tick; stats.ticks++; stats.minimumResolvedSpeed = Mathf.Min(stats.minimumResolvedSpeed, speed);
                    stats.maximumResolvedSpeed = Mathf.Max(stats.maximumResolvedSpeed, speed); stats.minimumDecisionSpeed = Mathf.Min(stats.minimumDecisionSpeed, decision);
                    stats.maximumDecisionSpeed = Mathf.Max(stats.maximumDecisionSpeed, decision); if (speed == 0f) stats.zeroHorizontalTicks++;
                    if (penetration > mover.SkinWidth + 0.015f) Issue("Capsule penetration beyond configured skin allowance at " + record.Tick + ":" + penetration);
                    if (sample.Facts.Any(f => !f.Succeeded)) Issue("Failed traversal fact at " + record.Tick);
                    ObserveCue(record, sample, leg); Progress(record, sample, leg);
                    if (!Done && record.Resolution.Position.y < -0.5f) Miss("below-floor");
                    if (!Done && samples.Count >= MaximumTicks) Miss("2400-tick-limit");
                    if (!Done && legTicks >= 480) Miss("480-tick-leg-limit");
                    if (!Done)
                    {
                        if (Vector3.Distance(record.Resolution.Position, progressPosition) >= 0.05f) { progressPosition = record.Resolution.Position; progressTick = samples.Count; }
                        else if (samples.Count - progressTick >= 90) Miss("90-tick-no-0.05m-progress");
                    }
                }
                catch (Exception error) { Fail("Record " + record.Tick + ": " + error); }
            }
            private CueBinding BindCue(Leg leg)
            {
                int marker = leg.Marker;
                if (leg.Name == "shared-101") marker = 101;
                else if (leg.Name == "lower-102") marker = 102;
                else if (leg.Name == "upper-103") marker = 103;
                var cue = new CueBinding { leg = leg.Name, marker = marker, position = leg.Target,
                    kind = "navigation", source = "frozen route waypoint", sourceObject = leg.Name,
                    crossingMeaning = "Original waypoint arrival region; no authored marker claim." };
                if (marker != 0)
                {
                    LevelMarker actual = markers[marker];
                    cue.kind = "marker"; cue.position = actual.transform.position; cue.source = "LevelMarker:" + marker;
                    cue.sourceObject = actual.name; cue.travelDirection = leg.Direction;
                    Collider collider = actual.GetComponent<Collider>();
                    cue.boundsCenter = collider == null ? cue.position : collider.bounds.center;
                    cue.boundsSize = collider == null ? actual.Capture().Size : collider.bounds.size;
                    if (leg.Kind == LegKind.Move)
                        cue.travelDirection = route.Name == "lower-low-west" || route.Name == "upper-west" ? Vector3.left : Vector3.right;
                    cue.crossingMeaning = leg.Kind == LegKind.Move ? "Recorded crossing of the actual portal marker plane." : "Actual authored interaction probe/fact; departure remains route-defined.";
                }
                else if (leg.Name == "ramp-to-deck")
                {
                    Collider ramp = geometry.Single(c => c.name == "Atrium Rising Line");
                    cue.kind = "authored-geometry"; cue.source = "Assets/Editor/Level/TagArenaLevelSetup.cs"; cue.sourceObject = ramp.name;
                    cue.position = ramp.bounds.center; cue.boundsCenter = ramp.bounds.center; cue.boundsSize = ramp.bounds.size;
                    cue.travelDirection = Vector3.right; cue.crossingMeaning = "Recorded crossing of the actual ramp bounds center; not its start.";
                }
                return cue;
            }
            private void TriggerCue(string label, long tick)
            {
                InputProbeRecord record = player.LastProbeRecord;
                Vector3 position = record.Resolution.Position;
                float signed = Vector3.Dot(Flat(position - currentCue.position), currentCue.travelDirection);
                report.cueObservations.Add(new CueObservation { label = label, leg = Current.Name, tick = tick,
                    kind = currentCue.kind, source = currentCue.source, sourceObject = currentCue.sourceObject, marker = currentCue.marker,
                    cuePosition = currentCue.position, playerPosition = position, signedPlaneDistance = signed,
                    horizontalDistance = Flat(position - currentCue.position).magnitude,
                    resolvedSpeed = Speed(record.Resolution.Velocity), controllerSpeed = Speed(player.ReadOnlyState.Velocity),
                    relation = currentCue.kind == "navigation" ? "navigation-region" : signed <= 0f ? "before-or-on-cue-plane" : "after-cue-plane" });
                observer.Trigger(label, tick);
            }
            private void ObserveCue(InputProbeRecord record, Sample sample, Leg leg)
            {
                if (!firstMovingImage && Speed(record.Resolution.Velocity) > 0f)
                { firstMovingImage = true; TriggerCue("route-first-moving-view", record.Tick); }
                float distance = Flat(currentCue.position - record.Resolution.Position).magnitude;
                float signed = Vector3.Dot(Flat(record.Resolution.Position - currentCue.position), currentCue.travelDirection);
                bool approaching = currentCue.kind == "navigation" || signed <= 0f;
                if (!fallbackImage && distance <= 6f && approaching)
                { fallbackImage = true; TriggerCue(leg.Name + "-first-nearby", record.Tick); }
                if (!approachImage && distance <= 6f && approaching && Speed(sample.DecisionVelocity) >= 7.5f)
                { approachImage = true; TriggerCue(leg.Name + "-ordinary-speed-approach", record.Tick); }
                bool interacting = leg.Kind == LegKind.Vault ? record.Probe.VaultCandidate && Vector3.Distance(record.Probe.VaultTarget, leg.Target) < 0.01f :
                    leg.Kind == LegKind.Rebound ? record.Probe.WallDetected && record.Probe.WallId == leg.Marker :
                    leg.Kind == LegKind.Gate ? record.Probe.StandingBlocked : leg.Kind == LegKind.Drop ? !record.Resolution.Grounded && record.Resolution.Position.x < 10f :
                    currentCue.kind != "navigation" && previousCueSigned < 0f && signed >= 0f;
                if (!interactionImage && interacting) { interactionImage = true; TriggerCue(leg.Name + "-interaction", record.Tick); }
                foreach (var fact in sample.Facts) TriggerCue(leg.Name + "-fact-" + fact.Kind + "-tick" + fact.Tick, record.Tick);
                previousCueSigned = signed;
            }
            private void Progress(InputProbeRecord record, Sample sample, Leg leg)
            {
                Vector3 p = record.Resolution.Position;
                if (leg.Kind == LegKind.Move)
                {
                    bool arrived = Flat(p - leg.Target).magnitude <= 0.8f;
                    if (route.Name == "vault201-south" && leg.Name == "vault-201-departure") arrived = p.z >= -2.5f;
                    if (arrived && Mathf.Abs(p.y - leg.Target.y) <= 0.35f && record.Resolution.Grounded) Advance(record.Tick);
                }
                else if (leg.Kind == LegKind.Vault)
                {
                    if (sample.Facts.Any(f => f.Kind == TraversalKind.Vault && f.Succeeded))
                    {
                        if (!requested || Vector3.Distance(p, leg.Target) > profile.VaultCompletionTolerance + 0.001f) Issue("Vault completed away from declared marker target " + leg.Marker);
                        Advance(record.Tick);
                    }
                }
                else if (leg.Kind == LegKind.Gate)
                {
                    Bounds b = markers[leg.Marker].GetComponent<Collider>().bounds;
                    if (p.x >= b.min.x && p.x <= b.max.x)
                        underGate |= requested && record.Resolution.Grounded && Mathf.Abs(capsule.height - mover.Height * mover.SlideHeightRatio) <= 0.001f && p.y + capsule.height < b.min.y;
                    standingBlocked |= record.Probe.StandingBlocked;
                    bool beyond = leg.Direction.x > 0f ? p.x > b.max.x + mover.Radius + 0.1f : p.x < b.min.x - mover.Radius - 0.1f;
                    if (beyond) { if (!underGate || !standingBlocked) Issue("Gate lacks actual low-capsule/standing-obstruction crossing " + leg.Marker); Advance(record.Tick); }
                }
                else if (leg.Kind == LegKind.Rebound)
                {
                    if (sample.Facts.Any(f => f.Kind == TraversalKind.Rebound && f.Succeeded)) reboundObserved = true;
                    if (reboundObserved && record.Resolution.Grounded && (leg.Direction.x > 0f ? p.x <= leg.Target.x : p.x >= leg.Target.x)) Advance(record.Tick);
                }
                else if (leg.Kind == LegKind.Drop)
                {
                    Bounds b = markers[106].GetComponent<Collider>().bounds;
                    if (!record.Probe.Grounded && !record.Resolution.Grounded && p.x < b.min.x && p.y > 0.5f) dropAir = true;
                    if (dropAir && record.Resolution.Grounded && Mathf.Abs(p.y) <= 0.06f && sample.Facts.Any(f => f.Kind == TraversalKind.Land && f.Succeeded)) Advance(record.Tick);
                }
            }
            private void Advance(long tick)
            {
                string old = Current.Name;
                if (!approachImage) report.missingSpeedViews.Add(old);
                TriggerCue(old + "-departure", tick); report.completedLegs++; legIndex++;
                report.transitions.Add(tick + ":" + old + "->" + (legIndex >= route.Legs.Length ? "finish" : Current.Name));
                if (legIndex >= route.Legs.Length) { report.termination = "first-declared-route-finish"; Done = true; }
                else BeginLeg();
            }
            private void Movement(PlayerMovementSample value)
            {
                if (closed) return;
                report.movementEvents++;
                if (samples.Count == 0 || !value.Equals(samples.Last().Movement)) Fail("Unmatched movement publication.");
            }
            private void Traversal(PlayerTraversalFact value) { if (!closed) { publishedFacts.Add(value); report.traversalEvents++; } }
            private void Committed(InputFrame frame, float dt, long tick)
            {
                if (closed) return;
                try
                {
                    Assert.That(tick, Is.EqualTo(samples.Count)); Assert.That(frame, Is.EqualTo(supplied)); Assert.That(dt, Is.EqualTo(Dt));
                    Assert.That(report.movementEvents, Is.EqualTo(samples.Count)); Assert.That(publishedFacts, Is.EqualTo(samples.SelectMany(s => s.Facts).ToArray()));
                }
                catch (Exception error) { Fail("Tick publication: " + error); }
                if (Done || Failed) Close();
            }
            private void Issue(string text) { if (!report.physicalIssues.Contains(text)) report.physicalIssues.Add(text); }
            private void Miss(string reason) { Issue(reason + " at tick " + samples.Count + " leg " + Current.Name); report.termination = reason; Done = true; }
            public void Fail(string reason) { if (!Failed) report.failure = reason; report.collectionIntegrityPassed = false; }
            public void PollImages() { if (observer != null) observer.Poll(); }
            public void Close()
            {
                if (closed) return; closed = true;
                try
                {
                    if (input != null && captures == 1 && samples.Count > 0)
                    {
                        Assert.That(input.SaveRecording(samples.Count, false), Is.True, input.LastRecordingError);
                        report.nativePath = input.LastRecordingPath; byte[] bytes = File.ReadAllBytes(report.nativePath); report.nativeSha256 = Hash(bytes);
                        File.WriteAllBytes(Path.Combine(folder, "original.incomplete.winput"), bytes);
                    }
                }
                catch (Exception error) { Fail("Native incomplete save: " + error); }
                finally
                {
                    if (run != null) run.SuspendForSceneLoad();
                    if (input != null && !string.IsNullOrEmpty(input.LastRecordingPath) && input.LastRecordingPath != report.nativePath)
                    { report.lifecyclePath = input.LastRecordingPath; byte[] bytes = File.ReadAllBytes(report.lifecyclePath); report.lifecycleSha256 = Hash(bytes); File.WriteAllBytes(Path.Combine(folder, "lifecycle-close.winput"), bytes); }
                    rows.Flush();
                }
            }
            public void Analyze()
            {
                if (analyzed) return; analyzed = true;
                bool analysisPassed = false;
                try
                {
                    observer?.Finish();
                    report.renderIntegrityPassed = observer != null && observer.IntegrityPassed;
                    report.missingCaptureUnits = observer == null ? 0 : observer.Missing;
                    if (observer != null) report.renderErrors.AddRange(observer.Errors);
                    string[] requestedLabels = observer == null ? Array.Empty<string>() : observer.RequestedLabels;
                    foreach (Leg leg in route.Legs)
                    {
                        foreach (string suffix in new[] { "-first-nearby", "-ordinary-speed-approach", "-departure" })
                            if (!requestedLabels.Contains(leg.Name + suffix)) report.missingCueLabels.Add(leg.Name + suffix);
                        if ((leg.Kind != LegKind.Move || report.cueBindings.Any(c => c.leg == leg.Name && c.kind != "navigation")) && !requestedLabels.Contains(leg.Name + "-interaction")) report.missingCueLabels.Add(leg.Name + "-interaction");
                    }
                    if (!report.renderIntegrityPassed) Fail("Render observer stream/capture error; see retained render report.");
                    VerifyFiles(); Assert.That(EditorJsonUtility.ToJson(profile), Is.EqualTo(profileSnapshot)); Assert.That(EditorJsonUtility.ToJson(mover), Is.EqualTo(moverSnapshot));
                    foreach (var pair in markers) Assert.That(EditorJsonUtility.ToJson(pair.Value), Is.EqualTo(markerStates[pair.Key]));
                    var expected = new List<TraversalKind>();
                    foreach (Leg leg in route.Legs)
                    { if (leg.Kind == LegKind.Vault) expected.Add(TraversalKind.Vault); if (leg.Kind == LegKind.Gate) expected.Add(TraversalKind.Slide); if (leg.Kind == LegKind.Rebound) { expected.Add(TraversalKind.Jump); expected.Add(TraversalKind.Rebound); } }
                    var facts = samples.SelectMany(s => s.Facts).ToArray();
                    if (!facts.Where(f => f.Kind != TraversalKind.Land).Select(f => f.Kind).SequenceEqual(expected)) Issue("Non-Land traversal facts differ from declared ordered verbs.");
                    Stationary stationary = null;
                    foreach (Sample sample in samples)
                    {
                        if (Speed(sample.Record.Resolution.Velocity) != 0f) { stationary = null; continue; }
                        string state = sample.Movement.MovementState.ToString();
                        if (stationary == null || stationary.leg != sample.Phase || stationary.state != state)
                        { stationary = new Stationary { leg = sample.Phase, state = state, firstTick = sample.Record.Tick }; report.stationary.Add(stationary); }
                        stationary.lastTick = sample.Record.Tick; stationary.seconds = (stationary.lastTick - stationary.firstTick + 1) * Dt;
                    }
                    ReplayOriginalIncomplete();
                    report.physicalRoutePassed = report.completedLegs == route.Legs.Length && report.physicalIssues.Count == 0 && facts.All(f => f.Succeeded);
                    analysisPassed = Done && !Failed && report.replayPassed;
                }
                catch (Exception error) { Fail("Analysis: " + error); }
                finally
                {
                    try
                    {
                        rows.Dispose();
                        report.tickRowsSha256 = Hash(File.ReadAllBytes(Path.Combine(folder, "ticks.jsonl")));
                    }
                    catch (Exception error) { Fail("Tick journal finalization: " + error); }
                    report.collectionIntegrityPassed = analysisPassed && !Failed;
                    report.status = report.collectionIntegrityPassed ? "intact-diagnostic-window" : "partial-or-integrity-failed";
                    report.finishedUtc = DateTime.UtcNow.ToString("O");
                    try { Write(Path.Combine(folder, "route-report.json"), report); }
                    catch (Exception error)
                    {
                        Fail("Route report finalization: " + error);
                        report.status = "partial-or-integrity-failed";
                    }
                }
            }
            private void ReplayOriginalIncomplete()
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(folder, "original.incomplete.winput"));
                Assert.That(new InputRecordingPresenter().TryDecode(bytes, out _, out _, out string rejection), Is.False);
                Assert.That(rejection, Does.Contain("incomplete"));
                var reconstructed = new InputReplayDriverState { Metadata = metadata, EndTick = samples.Count, CaptureComplete = false, CaptureInterrupted = true };
                reconstructed.Recorded.AddRange(samples.Select(s => s.Record));
                Assert.That(new InputRecordingPresenter().Encode(reconstructed), Is.EqualTo(bytes), "Native bytes must equal every retained source record.");
                var state = new PlayerBehaviorState(); var controller = new PlayerController(state, profile, new System.Random(1));
                controller.Reset(player.Id, route.Spawn, 90f);
                using (var reader = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8))
                {
                    Assert.That(reader.ReadInt32(), Is.EqualTo(0x57525031)); Assert.That(reader.ReadInt32(), Is.EqualTo(1));
                    var parsed = new RunCaptureMetadata(reader.ReadString(), reader.ReadInt32(), reader.ReadSingle(), reader.ReadString(), reader.ReadString(), reader.ReadString(), reader.ReadInt64());
                    Assert.That(parsed, Is.EqualTo(metadata)); Assert.That(reader.ReadInt64(), Is.EqualTo(samples.Count)); Assert.That(reader.ReadBoolean(), Is.False);
                    Assert.That(reader.ReadInt32(), Is.EqualTo(samples.Count));
                    for (int i = 0; i < samples.Count; i++)
                    {
                        InputProbeRecord decoded = ReadDiagnosticRecord(reader); Sample expected = samples[i]; Assert.That(decoded, Is.EqualTo(expected.Record));
                        controller.Replay(decoded); Assert.That(state.Position, Is.EqualTo(expected.Movement.Position));
                        Assert.That(state.Velocity, Is.EqualTo(expected.DecisionVelocity)); Assert.That(state.LastMovementSample, Is.EqualTo(expected.Movement));
                        Assert.That(state.LastTraversalFacts.ToArray(), Is.EqualTo(expected.Facts)); report.replayRecords++;
                    }
                    Assert.That(reader.BaseStream.Position, Is.EqualTo(reader.BaseStream.Length));
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
                Close(); observer?.Finish();
                TagArenaSceneRoot.SceneReady -= SceneReady;
                if (captureRun != null) captureRun.CaptureStarted -= CaptureStarted;
                if (input != null) input.FramePublished -= Supply;
                if (run != null) { run.PlayerProbeRecorded -= Record; run.PlayerMovementPublished -= Movement; run.PlayerTraversalPublished -= Traversal; run.TickAdvanced -= Committed; }
                if (restoreDevices && gameplay != null) gameplay.devices = previousDevices;
                if (restoreRoot && root != null) { Field(root, "_spawnPosition").SetValue(root, originalSpawn); Field(root, "_seed").SetValue(root, originalSeed); }
                foreach (var hunter in disabledHunters) if (hunter != null) hunter.SetActive(true);
                rows.Dispose();
            }
        }
        private static InputProbeRecord ReadDiagnosticRecord(BinaryReader reader)
        {
            int schema = reader.ReadInt32(); long tick = reader.ReadInt64(); float dt = reader.ReadSingle();
            var frame = new InputFrame(new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                new Vector2(reader.ReadSingle(), reader.ReadSingle()), (InputButtons)reader.ReadInt32(),
                (InputButtons)reader.ReadInt32(), (InputButtons)reader.ReadInt32());
            var probe = new MovementProbe(reader.ReadBoolean(), ReadVector(reader), reader.ReadBoolean(),
                reader.ReadSingle(), ReadVector(reader), reader.ReadSingle(), reader.ReadInt32(), reader.ReadBoolean(),
                reader.ReadSingle(), reader.ReadSingle(), ReadVector(reader), reader.ReadBoolean());
            bool present = reader.ReadBoolean();
            var position = ReadVector(reader); var velocity = ReadVector(reader);
            bool grounded = reader.ReadBoolean(); bool ceiling = reader.ReadBoolean(); var eye = ReadVector(reader);
            var resolution = present ? new MovementResolution(position, velocity, grounded, ceiling, eye) : default;
            return new InputProbeRecord(schema, tick, frame, probe, dt, resolution);
        }

        private static Vector3 ReadVector(BinaryReader reader) => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        private static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0f, value.z);

        private static T One<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None).Single();
        private static FieldInfo Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(owner.GetType().Name, name);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Speed(Vector3 velocity) => (float)Math.Sqrt((double)velocity.x * velocity.x + (double)velocity.z * velocity.z);
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
        // Required usings: System; System.Collections.Generic; System.IO;
        // System.Linq; System.Security.Cryptography; System.Text; UnityEditor;
        // UnityEngine; UnityEngine.Rendering; Worsen.Core; Worsen.Domain.Player;
        // Worsen.Session.Run;
        // using CameraDriver = Worsen.Presentation.Camera.CameraDriver;
        // Test-only passive observation. Construct after readiness; suspend the
        // simulation before draining Pending via Poll for at most two real seconds.
        // Finish always detaches; missing images remain physical evidence gaps.
        private sealed class RenderObserver
        {
            private readonly RunSessionManager run;
            private readonly PlayerManager player;
            private readonly Func<string> phase;
            private readonly RenderObserverReport report;
            private readonly RenderImageAdmission admission;
            private StreamWriter frames;
            private UnityEngine.Camera outputCamera;
            private bool attached, finished;
            private long renderSequence;

            public RenderObserver(RunSessionManager run, PlayerManager player, string folder, Func<string> phase, HashSet<int> captureFrames)
            {
                this.run = run; this.player = player; this.phase = phase;
                report = new RenderObserverReport { folder = Path.GetFullPath(folder), startedUtc = DateTime.UtcNow.ToString("o") };
                report.framesPath = Path.Combine(report.folder, "render-frames.jsonl");
                admission = new RenderImageAdmission(report, captureFrames ?? throw new ArgumentNullException(nameof(captureFrames)));
                try
                {
                    Directory.CreateDirectory(report.folder);
                    frames = new StreamWriter(new FileStream(report.framesPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
                }
                catch (Exception error) { Error("Create render stream", error); }
                try
                {
                    report.scenePath = player.gameObject.scene.path;
                    CameraDriver[] cameras = UnityEngine.Object.FindObjectsByType<CameraDriver>(FindObjectsSortMode.None)
                        .Where(item => item.gameObject.scene.path == report.scenePath).ToArray();
                    report.authoredCameraDriverCount = cameras.Length;
                    if (cameras.Length != 1)
                    { report.missingCameraReason = "Expected exactly one authored CameraDriver in the Player scene; found " + cameras.Length; return; }
                    using (var fields = new SerializedObject(cameras[0]))
                    {
                        SerializedProperty property = fields.FindProperty("_outputCamera");
                        if (property == null) { report.missingCameraReason = "CameraDriver has no serialized _outputCamera property."; return; }
                        outputCamera = property.objectReferenceValue as UnityEngine.Camera;
                    }
                    if (outputCamera == null)
                    { report.missingCameraReason = "Authored CameraDriver has no output camera."; return; }
                    report.cameraName = outputCamera.name; report.cameraId = outputCamera.GetInstanceID();
                    report.cameraEnabledAtAttachment = outputCamera.isActiveAndEnabled;
                    RenderPipelineManager.endCameraRendering += ObserveCamera;
                    attached = true;
                }
                catch (Exception error) { Error("Attach render observer", error); }
            }

            public bool IntegrityPassed => report.errors.Count == 0;
            public string[] RequestedLabels => admission.RequestedLabels;
            public bool Pending => admission.Pending;
            // Units are missing physical images plus unassigned unique labels.
            // Per-label coverage is reported separately, without double counting.
            public int Missing => admission.Missing;
            public IReadOnlyList<string> Errors => report.errors;

            public void Trigger(string label, long triggerTick)
            {
                var trigger = new RenderLabelRequest { label = label ?? "", triggerTick = triggerTick,
                    triggerFrame = Time.frameCount, triggerObservedTick = run == null ? -1 : run.Tick,
                    triggerRealtimeSeconds = Time.realtimeSinceStartupAsDouble, triggerUtc = DateTime.UtcNow.ToString("o"),
                    triggerPhase = ReadPhase(), triggerLastRenderSequence = renderSequence };
                admission.Add(trigger);
                if (finished)
                {
                    trigger.rejected = true; trigger.rejectionReason = "Trigger occurred after Finish; no capture performed.";
                    Error("Trigger after Finish", new InvalidOperationException(trigger.label));
                }
            }

            private void ObserveCamera(ScriptableRenderContext context, UnityEngine.Camera camera)
            {
                if (finished || camera == null || camera != outputCamera) return;
                RenderFrameObservation observation;
                try
                {
                    report.selectedRenderCount++;
                    observation = Snapshot(++renderSequence);
                    if (frames != null) { frames.WriteLine(JsonUtility.ToJson(observation)); report.writtenRenderCount++; }
                    else Error("Write render observation", new IOException("Render stream unavailable at render " + renderSequence));
                }
                catch (Exception error) { Error("Observe selected camera render " + renderSequence, error); return; }
                foreach (RenderImageRequest request in report.requests)
                {
                    if (request.firstSubsequentRender == null && observation.sequence > request.request.sequence)
                        request.firstSubsequentRender = observation;
                    PollPng(request, observation, "selected camera endCameraRendering");
                }
                // Reserve once before invoking the asynchronous API. A thrown
                // call still consumes this frame and is never silently retried.
                RenderImageRequest batch = admission.Admit(observation);
                if (batch == null) return;
                if (outputCamera.targetTexture != null || outputCamera.cameraType != CameraType.Game)
                {
                    batch.requestRejected = true;
                    batch.rejectionReason = "Selected authored camera is not a direct Game camera backbuffer; no camera override performed.";
                    return;
                }
                try
                {
                    if (File.Exists(batch.path)) throw new IOException("Refusing to overwrite original PNG: " + batch.path);
                    batch.captureCallAttempted = true;
                    // Exactly one original deferred Game View PNG for this frame;
                    // every queued label references this same physical request.
                    ScreenCapture.CaptureScreenshot(batch.path, 1);
                    batch.captureCallReturned = true;
                }
                catch (Exception error)
                {
                    batch.requestRejected = true; batch.rejectionReason = error.ToString();
                    // A missing individual image is retained as a visual gap.
                }
            }

            private RenderFrameObservation Snapshot(long sequence)
            {
                PlayerMovementSample movement = player.LastMovementSample;
                InputProbeRecord record = player.LastProbeRecord;
                return new RenderFrameObservation { sequence = sequence, frame = Time.frameCount, tick = run.Tick,
                    movementTick = movement.Tick, probeTick = record.Tick, utc = DateTime.UtcNow.ToString("o"), phase = ReadPhase(),
                    realtimeSeconds = Time.realtimeSinceStartupAsDouble, presentationSeconds = Time.timeAsDouble, presentationDelta = Time.deltaTime,
                    movement = movement.MovementState.ToString(), movementPosition = movement.Position, movementVelocity = movement.Velocity,
                    decisionVelocity = player.ReadOnlyState.Velocity, resolvedPosition = record.Resolution.Position, resolvedVelocity = record.Resolution.Velocity,
                    resolvedEye = record.Resolution.EyePosition, grounded = record.Resolution.Grounded, ceiling = record.Resolution.Ceiling,
                    heading = movement.HeadingDegrees, inputLockSeconds = movement.InputLockSeconds, lookBack = movement.LookBack,
                    inputHeld = (int)record.Input.Held, inputPressed = (int)record.Input.Pressed, inputReleased = (int)record.Input.Released,
                    inputMove = record.Input.Move, inputLook = record.Input.LookDelta, inputDeltaTime = record.DeltaTime,
                    cameraId = outputCamera.GetInstanceID(), cameraPosition = outputCamera.transform.position,
                    cameraRotation = outputCamera.transform.rotation, cameraForward = outputCamera.transform.forward, cameraUp = outputCamera.transform.up,
                    verticalFov = outputCamera.fieldOfView, aspect = outputCamera.aspect, nearClip = outputCamera.nearClipPlane, farClip = outputCamera.farClipPlane,
                    cullingMask = outputCamera.cullingMask, pixelRect = outputCamera.pixelRect, screenWidth = Screen.width, screenHeight = Screen.height,
                    targetDisplay = outputCamera.targetDisplay, targetTexture = outputCamera.targetTexture == null ? "none" : outputCamera.targetTexture.name,
                    projection = outputCamera.projectionMatrix, worldToCamera = outputCamera.worldToCameraMatrix };
            }

            public void Poll()
            {
                if (finished) return;
                foreach (RenderImageRequest request in report.requests)
                    PollPng(request, null, "fixture polling outside render callback");
            }

            private void PollPng(RenderImageRequest request, RenderFrameObservation observation, string observationPhase)
            {
                if (!request.captureCallAttempted || request.fileComplete || !string.IsNullOrEmpty(request.fileError) || !File.Exists(request.path)) return;
                try
                {
                    using (var stream = new FileStream(request.path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        if (stream.Length < 45) return;
                        byte[] header = new byte[24], tail = new byte[12];
                        if (stream.Read(header, 0, header.Length) != header.Length) return;
                        byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
                        if (!header.Take(8).SequenceEqual(signature)) throw new InvalidDataException("PNG signature mismatch.");
                        stream.Seek(-12, SeekOrigin.End);
                        if (stream.Read(tail, 0, tail.Length) != tail.Length) return;
                        byte[] iend = { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
                        if (!tail.SequenceEqual(iend)) return;
                        request.width = PngBigEndian(header, 16); request.height = PngBigEndian(header, 20);
                        if (request.width <= 0 || request.height <= 0) throw new InvalidDataException("PNG has invalid dimensions.");
                        request.bytes = stream.Length; stream.Position = 0;
                        using (var sha = SHA256.Create()) request.sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                        request.fileComplete = true;
                    }
                    request.observedComplete = observation; request.fileObservedUtc = DateTime.UtcNow.ToString("o");
                    request.fileObservedPhase = observationPhase; request.fileObservedFrame = Time.frameCount;
                    request.fileObservedTick = run == null ? -1 : run.Tick; request.fileObservedRealtimeSeconds = Time.realtimeSinceStartupAsDouble;
                }
                catch (IOException error)
                {
                    // An asynchronous writer can legitimately hold the file. Keep
                    // the pending request and every occurrence in the report.
                    request.deferredReadErrors.Add(DateTime.UtcNow.ToString("o") + ": " + error.Message);
                }
                catch (Exception error) { request.fileError = error.ToString(); }
            }

            public void Finish()
            {
                if (finished) return;
                if (attached) { RenderPipelineManager.endCameraRendering -= ObserveCamera; attached = false; }
                Poll(); finished = true;
                try { frames?.Dispose(); frames = null; }
                catch (Exception error) { Error("Close render stream", error); }
                report.finishedUtc = DateTime.UtcNow.ToString("o"); report.finishTick = run == null ? -1 : run.Tick;
                report.missingCaptureUnits = Missing;
                report.missingPhysicalImages = admission.MissingPhysicalImages;
                report.unassignedUniqueLabels = admission.UnassignedUniqueLabels;
                report.uniqueLabelsWithoutCompleteImage = admission.UncoveredUniqueLabels;
                report.duplicateLabels = report.labels.Count(item => item.duplicateOfLabel != 0);
                report.requestsWithoutSubsequentRender = report.requests.Count(item => item.request != null && item.firstSubsequentRender == null);
                try
                {
                    if (File.Exists(report.framesPath)) report.framesSha256 = RenderHash(report.framesPath);
                }
                catch (Exception error) { Error("Hash finished render stream", error); }
                foreach (RenderImageRequest request in report.requests.Where(item => item.fileComplete))
                {
                    try
                    {
                        request.finishSha256 = RenderHash(request.path);
                        request.originalUnchangedAtFinish = request.finishSha256 == request.sha256;
                        if (!request.originalUnchangedAtFinish) Error("Preserve original PNG " + request.sequence, new InvalidDataException("PNG changed after first complete observation."));
                    }
                    catch (Exception error) { Error("Hash finished PNG " + request.sequence, error); }
                }
                report.integrityPassed = IntegrityPassed;
                try { File.WriteAllText(Path.Combine(report.folder, "render-report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false)); }
                catch (Exception error) { Error("Write render report", error); }
            }

            private string ReadPhase()
            { try { return phase == null ? "" : phase() ?? ""; } catch (Exception error) { Error("Read route phase", error); return "phase-read-failed"; } }
            private void Error(string operation, Exception error) => report.errors.Add(DateTime.UtcNow.ToString("o") + " " + operation + ": " + error);
            private static string RenderHash(string path)
            { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""); }
            private static int PngBigEndian(byte[] value, int offset) => (value[offset] << 24) | (value[offset + 1] << 16) | (value[offset + 2] << 8) | value[offset + 3];
        }

        // Pure metadata admission. Tests exercise this exact helper without
        // cameras, file creation, asynchronous capture, or simulation changes.
        private sealed class RenderImageAdmission
        {
            private readonly RenderObserverReport report;
            private readonly Dictionary<string, RenderLabelRequest> unique = new Dictionary<string, RenderLabelRequest>(StringComparer.Ordinal);
            private readonly HashSet<int> reservedFrames;
            public RenderImageAdmission(RenderObserverReport report, HashSet<int> captureFrames = null)
            { this.report = report; reservedFrames = captureFrames ?? new HashSet<int>(); }
            public string[] RequestedLabels => unique.Keys.ToArray();
            public int UnassignedUniqueLabels => unique.Values.Count(item => item.imageSequence == 0);
            public int MissingPhysicalImages => report.requests.Count(item => !item.fileComplete);
            public int Missing => MissingPhysicalImages + UnassignedUniqueLabels;
            public int UncoveredUniqueLabels => unique.Values.Count(item => item.imageSequence == 0 || !report.requests[item.imageSequence - 1].fileComplete);
            public bool Pending => unique.Values.Any(item => item.imageSequence == 0 && !item.rejected) ||
                report.requests.Any(item => !item.fileComplete && !item.requestRejected && string.IsNullOrEmpty(item.fileError));

            public void Add(RenderLabelRequest trigger)
            {
                trigger.sequence = report.labels.Count + 1;
                report.labels.Add(trigger);
                if (unique.TryGetValue(trigger.label, out RenderLabelRequest original))
                {
                    trigger.duplicateOfLabel = original.sequence;
                    trigger.rejectionReason = "Duplicate label retained; resolve its original label record for image association. No retry.";
                }
                else unique.Add(trigger.label, trigger);
            }

            public RenderImageRequest Admit(RenderFrameObservation observation)
            {
                RenderLabelRequest[] queued = report.labels.Where(item => item.duplicateOfLabel == 0 && item.imageSequence == 0 && !item.rejected).ToArray();
                if (queued.Length == 0) return null;
                if (reservedFrames.Contains(observation.frame))
                {
                    report.sameFrameDeferrals.Add(new RenderBatchDeferral { frame = observation.frame, tick = observation.tick,
                        renderSequence = observation.sequence, labelSequences = queued.Select(item => item.sequence).ToArray(),
                        realtimeSeconds = observation.realtimeSeconds, observation = observation });
                    return null;
                }
                reservedFrames.Add(observation.frame);
                var batch = new RenderImageRequest { sequence = report.requests.Count + 1, request = observation,
                    labelSequences = queued.Select(item => item.sequence).ToArray(), labels = queued.Select(item => item.label).ToArray() };
                batch.path = Path.Combine(report.folder, "image-" + batch.sequence.ToString("000") + "-frame" + observation.frame + "-tick" + observation.tick + ".png");
                report.requests.Add(batch);
                foreach (RenderLabelRequest trigger in queued)
                {
                    trigger.imageSequence = batch.sequence;
                    trigger.requestFrameLag = observation.frame - trigger.triggerFrame;
                    trigger.requestTickLag = observation.tick - trigger.triggerTick;
                    trigger.requestObservedTickLag = observation.tick - trigger.triggerObservedTick;
                    trigger.requestRenderSequenceLag = observation.sequence - trigger.triggerLastRenderSequence;
                    trigger.requestRealtimeLag = observation.realtimeSeconds - trigger.triggerRealtimeSeconds;
                }
                return batch;
            }
        }

        [Serializable] private sealed class RenderObserverReport
        {
            public string folder, framesPath, framesSha256, scenePath, startedUtc, finishedUtc, cameraName, missingCameraReason;
            public int cameraId, authoredCameraDriverCount, missingCaptureUnits, missingPhysicalImages, unassignedUniqueLabels,
                uniqueLabelsWithoutCompleteImage, duplicateLabels, requestsWithoutSubsequentRender;
            public long finishTick, selectedRenderCount, writtenRenderCount;
            public bool cameraEnabledAtAttachment, integrityPassed;
            public string scope = "Passive actual output-camera observation. Every selected render is streamed without a cap. A shared cohort frame budget permits at most one physical PNG request per selected render and per Unity frame across route handoffs; all labels waiting at that render share it. Later same-frame labels wait for the next eligible render. No simulation, camera, target, or pixel changes. MissingCaptureUnits counts missing physical PNGs plus unique labels not assigned to an image; per-label coverage is separate. PNG identity is not readability acceptance.";
            public List<string> errors = new List<string>();
            public List<RenderImageRequest> requests = new List<RenderImageRequest>();
            public List<RenderLabelRequest> labels = new List<RenderLabelRequest>();
            public List<RenderBatchDeferral> sameFrameDeferrals = new List<RenderBatchDeferral>();
        }

        [Serializable] private sealed class RenderLabelRequest
        {
            public int sequence, duplicateOfLabel, triggerFrame, imageSequence, requestFrameLag;
            public long triggerTick, triggerObservedTick, triggerLastRenderSequence, requestTickLag, requestObservedTickLag, requestRenderSequenceLag;
            public double triggerRealtimeSeconds, requestRealtimeLag;
            public string label, triggerUtc, triggerPhase, rejectionReason;
            public bool rejected;
            public string scope = "Exact trigger metadata; imageSequence associates a unique label with its later physical capture request. Lag is retained. Trigger phase and render-request phase are separate metadata: a phase change after a waypoint does not itself establish that the Player has turned. Actual heading and input remain in the render observation. A duplicate label only references the original label record; it never implies that a new image was taken for its later trigger.";
        }

        [Serializable] private sealed class RenderBatchDeferral
        {
            public int frame;
            public long tick, renderSequence;
            public double realtimeSeconds;
            public int[] labelSequences;
            public RenderFrameObservation observation;
        }

        [Serializable] private sealed class RenderImageRequest
        {
            public int sequence, width, height, fileObservedFrame;
            public int[] labelSequences;
            public string[] labels;
            public long bytes, fileObservedTick;
            public double fileObservedRealtimeSeconds;
            public string path, rejectionReason, sha256, finishSha256, fileError, fileObservedUtc, fileObservedPhase;
            public bool requestRejected, captureCallAttempted, captureCallReturned, fileComplete, originalUnchangedAtFinish;
            public RenderFrameObservation request, firstSubsequentRender, observedComplete;
            public List<string> deferredReadErrors = new List<string>();
            public string timing = "One physical deferred full Game View PNG request for one selected render/frame. All associated labels share this original file; their exact trigger metadata and lag remain in the label records. Request render, first subsequent selected render and file-completion observation are separate. A later render may share the engine frame; sequence disambiguates callbacks. Disk-write time is not pixel time. No retry after a rejected/missing image.";
        }

        [Serializable] private sealed class RenderFrameObservation
        {
            public long sequence, tick, movementTick, probeTick;
            public int frame, inputHeld, inputPressed, inputReleased, cameraId, cullingMask, screenWidth, screenHeight, targetDisplay;
            public double realtimeSeconds, presentationSeconds;
            public float presentationDelta, inputDeltaTime, heading, inputLockSeconds, verticalFov, aspect, nearClip, farClip;
            public bool grounded, ceiling, lookBack;
            public string utc, phase, movement, targetTexture;
            public Vector2 inputMove, inputLook;
            public Vector3 movementPosition, movementVelocity, decisionVelocity, resolvedPosition, resolvedVelocity, resolvedEye, cameraPosition, cameraForward, cameraUp;
            public Quaternion cameraRotation;
            public Rect pixelRect;
            public Matrix4x4 projection, worldToCamera;
        }

    }
}
