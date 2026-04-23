/**
 * 模拟报文数据与服务
 * 用于在无后端环境下模拟 MES API 返回值及硬件交互
 */

import type { GetRouteResponse, OrderInfo } from '../types/mes'

// 1. 模拟工单数据
export const MOCK_ORDER_INFO: OrderInfo = {
  orderCode: 'WO20240419001',
  route_No: 'ROUTE_BASE_001',
  product_Model: 'T-X1000',
  material_No: 'MAT-FIN-001',
  material_Name: '完成品主机',
  plan_Qty: 100,
  order_Status: '1'
}

// 2. 模拟工艺路线/工步数据 (从 模拟报文.txt 整理而来)
export const MOCK_ROUTE_DATA: GetRouteResponse = {
  code: 200,
  msg: 'success',
  data: {
    workSeqList: [
      {
        workseqNo: '10',
        workseqName: '物料校验工序',
        workStepList: [
          {
            workstepNo: '10-1',
            workstepName: '扫描底板条码',
            workstepType: 0,
            workStepMaterialList: [
              {
                material_No: 'MAT-BASE-001',
                material_Name: '底板',
                material_number: 1,
                noLength: 12,
                retrospect_Type: '0',
                isCheckLength: '1',
                material_id: 'M001'
              }
            ]
          }
        ]
      },
      {
        workseqNo: '20',
        workseqName: '定扭拧紧工序',
        workStepList: [
          {
            workstepNo: '20-1',
            workstepName: 'M6螺丝拧紧',
            workstepType: 1,
            torqueSettingCount: 4,
            workStepParamList: [
              {
                paramName: '扭矩',
                minQualityValue: 1.0,
                maxQualityValue: 2.0,
                standardValue: 1.5,
                paramUnit: 'Nm'
              },
              {
                paramName: '角度',
                minQualityValue: 30,
                maxQualityValue: 60,
                standardValue: 45,
                paramUnit: 'Deg'
              }
            ],
            workStepLineList: [
              {
                pSetNo: '01',
                torqueSettingCount: 4
              }
            ]
          }
        ]
      }
    ]
  }
}

/**
 * 模拟硬件输入工具
 */
export const MockHardware = {
  /**
   * 模拟扫描物料
   * @param materialNo 物料前缀
   * @param length 长度
   */
  generateBarcode(materialNo: string, length: number): string {
    const randomStr = Math.random().toString(36).substring(2, 2 + Math.max(0, length - materialNo.length))
    return (materialNo + randomStr).substring(0, length).toUpperCase()
  },

  /**
   * 模拟定扭结果
   * @param standard 目标值
   * @param range 浮动范围
   */
  simulateTorqueResult(standard: number, min: number, max: number) {
    // 80% 概率合格
    const isPass = Math.random() > 0.2
    let torque: number
    let angle: number

    if (isPass) {
      torque = min + Math.random() * (max - min)
      angle = 35 + Math.random() * 20
    } else {
      torque = Math.random() > 0.5 ? max + 0.1 : min - 0.1
      angle = 20 + Math.random() * 50
    }

    return {
      torque: torque.toFixed(3),
      angle: angle.toFixed(1),
      status: isPass ? 'OK' : 'NG'
    }
  }
}
