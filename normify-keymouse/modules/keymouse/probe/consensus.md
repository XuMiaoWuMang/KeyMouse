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
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:31:02.527Z"
fingerprint: c8a37194838b7eb26afe170b7729b9947ba9a014cdcb0881b2b4e87d9c1aa6aa
source:
  - path: "Probe.cs"
    line: 224
    end_line: 251
apis:
  - protocol: rpc
    path: "Probe.IsSeen"
    description:
      zh: >
          把“到底看清了没”的策略集中在一处可单测的地方：多次读必须一致，置信度需过地板。
          
      en: >
          The whole did-we-see-it policy in one testable place: reads must agree, and confidence must clear the floor.
          
---
