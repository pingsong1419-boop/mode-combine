// MES API 请求服务

import type {
  AppConfig,
  GetOrderRequest,
  GetOrderResponse,
  GetRouteRequest,
  GetRouteResponse
} from '../types/mes'
import { BACKEND_URL } from '../utils/constants'

/**
 * 通用POST请求，支持跨域
 */
async function postRequest<T>(url: string, body: object): Promise<T> {
  const proxyUrl = `${BACKEND_URL}/api/proxy?url=${encodeURIComponent(url)}`
  console.log(`[Proxy Request] Target: ${url}`)
  
  const controller = new AbortController()
  const timeoutId = setTimeout(() => controller.abort(), 10000)

  try {
    const response = await fetch(proxyUrl, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
      signal: controller.signal
    })
    clearTimeout(timeoutId)

    if (!response.ok) {
      const text = await response.text()
      throw new Error(`代理错误 (${response.status}): ${text}`)
    }

    const data = await response.json()
    return data as T
  } catch (err: any) {
    clearTimeout(timeoutId)
    console.error('[Proxy Error]', err)
    throw err
  }
}

/**
 * 步骤一：通过非首段工序获取工单信息
 * POST /api/0rderInfo/GetOtherOrderInfoByProcess
 */
export async function getOrderByProcess(
  config: AppConfig,
  _code: string // 保留签名以兼容调用处，但不使用
): Promise<GetOrderResponse> {
  const params: GetOrderRequest = {
    produce_Type: config.produce_Type,
    tenantID: config.tenantID
  }
  return postRequest<GetOrderResponse>(config.orderApiUrl, params)
}

/**
 * 步骤二：根据routeCode获取工步列表
 * POST /api/0rderInfo/GetTechRouteListByCode
 */
export async function getRouteList(
  config: AppConfig,
  routeCode: string
): Promise<GetRouteResponse> {
  const params: GetRouteRequest = {
    routeCode,
    workSeqNo: config.technicsProcessCode
  }
  return postRequest<GetRouteResponse>(config.routeApiUrl, params)
}
/**
 * 步骤三：单物料校验
 * @param apiUrl 校验接口地址
 * @param body 校验请求体
 */
export async function checkSingleMaterial(apiUrl: string, body: object): Promise<any> {
  return postRequest<any>(apiUrl, body)
}

/**
 * 步骤四：重码校验
 * @param apiUrl 校验接口地址
 * @param body 校验请求体
 */
export async function checkDuplicateBarcode(apiUrl: string, body: object): Promise<any> {
  return postRequest<any>(apiUrl, body)
}
/**
 * 步骤五：获取电芯详细数据 (NJ专用)
 * @param apiUrl 接口地址
 * @param body 请求体
 */
export async function getCellData(apiUrl: string, body: object): Promise<any> {
  return postRequest<any>(apiUrl, body)
}
/**
 * 步骤六：推送生产结果数据到 MES
 * @param apiUrl 推送接口地址
 * @param body 请求体 (包含条码、判定结果、过程参数等)
 */
export async function pushToMes(apiUrl: string, body: object): Promise<any> {
  return postRequest<any>(apiUrl, body)
}
/**
 * 步骤七：生成模块码
 * @param apiUrl 接口地址
 * @param body 请求体
 */
export async function createModuleCode(apiUrl: string, body: object): Promise<any> {
  return postRequest<any>(apiUrl, body)
}

/**
 * 步骤八：写入 PLC 数据
 * @param body { dbNum: number, address: string, value: any }
 */
export async function writePlcValue(body: object): Promise<any> {
  const url = `${BACKEND_URL}/api/plc/write`
  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  })
  if (!response.ok) throw new Error('写入 PLC 失败')
  return response.json()
}
