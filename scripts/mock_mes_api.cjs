/**
 * MES API 模拟服务器 (Mock MES HTTP API)
 * 模拟接口: 
 * 1. 非首站获取工单 (GetOtherOrderInfoByProcess)
 * 2. 工步下发/工艺路线 (GetTechRouteListByCode)
 */
const http = require('http');

const PORT = 8076; // 模拟 MES 端口

const mockOrderResponse = {
    code: 200,
    msg: "success",
    datas: [{
        orderCode: "ORD-2026-001",
        route_No: "ROUTE-CTP-99",
        productCode: "PROD-MOCK-001",
        status: "Released",
        materialName: "CTP电池包"
    }]
};

const mockRouteResponse = {
    code: 200,
    msg: "success",
    data: {
        workSeqList: [
            {
                workseqNo: "10",
                workseqName: "物料校验工序",
                workStepList: [
                    {
                        workstepNo: "10-1",
                        workstepName: "扫描底板条码",
                        workstepType: 0,
                        workStepMaterialList: [
                            { material_No: "MAT-BASE-001", material_Name: "底板", material_number: 1, noLength: 12 }
                        ]
                    }
                ]
            },
            {
                workseqNo: "20",
                workseqName: "定扭拧紧工序",
                workStepList: [
                    {
                        workstepNo: "20-1",
                        workstepName: "M6螺丝拧紧",
                        workstepType: 1,
                        torqueSettingCount: 4, // 模拟4颗螺丝
                        workStepParamList: [
                            { paramName: "扭矩", minQualityValue: 1.0, maxQualityValue: 2.0, standardValue: 1.5, paramUnit: "Nm" },
                            { paramName: "角度", minQualityValue: 30, maxQualityValue: 60, standardValue: 45, paramUnit: "Deg" }
                        ]
                    }
                ]
            }
        ]
    }
};

const server = http.createServer((req, res) => {
    // 设置跨域和响应头
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'POST, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
    res.setHeader('Content-Type', 'application/json; charset=utf-8');

    if (req.method === 'OPTIONS') {
        res.writeHead(204);
        res.end();
        return;
    }

    let body = '';
    req.on('data', chunk => { body += chunk; });
    req.on('end', () => {
        console.log(`[Mock MES] 收到请求: ${req.url} | Body: ${body}`);

        if (req.url.includes('GetOtherOrderInfoByProcess')) {
            res.end(JSON.stringify(mockOrderResponse));
        } else if (req.url.includes('GetTechRouteListByCode')) {
            res.end(JSON.stringify(mockRouteResponse));
        } else {
            res.writeHead(404);
            res.end(JSON.stringify({ code: 404, msg: "Not Found" }));
        }
    });
});

server.listen(PORT, () => {
    console.log(`\n=========================================`);
    console.log(`  MES API 模拟服务器 (HTTP Mock)         `);
    console.log(`  监听地址: http://127.0.0.1:${PORT}      `);
    console.log(`  模拟接口:                             `);
    console.log(`  - /api/OrderInfo/GetOtherOrderInfoByProcess `);
    console.log(`  - /api/OrderInfo/GetTechRouteListByCode     `);
    console.log(`=========================================\n`);
});
