---
uid: fc196de5
id: keymouse.probe.consensus
parent: keymouse.probe
tags: [perception, ocr]
name: {zh: "共识判定与置信度地板", en: "Consensus and confidence floor"}
description:
  zh: >
      N 次重新采集必须逐字一致才算“看清”，置信度还需过 30 的地板。两个数字都是实测定的：合成图上 60 以下全是垃圾，但真实屏幕上一次读对的只有 55.8，所以地板降到只抓彻底失败；闪烁的光标只从比较里剔除，绝不从输出里剔除。
      
  en: >
      N re-captures must agree character for character before a read counts as seen, and confidence must clear a floor of 30. Both numbers are measured: everything under 60 was garbage on synthetic images, but a correct real read scored 55.8, so the floor moved down to catch only outright failures; a blinking caret is stripped from the comparison, never from the output.
      
revision: 7a1a124eb7b20fcbebed310d012cbf3926b69a0b
updated_at: "2026-10-03T08:34:07.493Z"
fingerprint: b104dda83feba9f56879cb7257c7cd4faa7d2db99e1d0d589c89be55c65a61ae
source:
  - path: "Probe.cs"
    line: 265
    end_line: 292
apis:
  - protocol: rpc
    path: "Probe.IsSeen"
    description:
      zh: >
          把“到底看清了没”的策略集中在一处可单测的地方：多次读必须一致，置信度需过地板。
          
      en: >
          The whole did-we-see-it policy in one testable place: reads must agree, and confidence must clear the floor.
          
---
