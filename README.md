# daorsonhome 쇼핑몰 백엔드

대박비품 쇼핑몰을 위한 ASP.NET Core MVC 백엔드입니다. Supabase의 Postgres 데이터베이스를 서버에서 호출하며, 비밀 키는 브라우저에 전달하지 않습니다. 대표 도메인은 `https://daorson.co.kr`입니다.

## API

- `GET /api/health` — 앱 상태 확인
- `GET /api/products` — 판매 중인 상품 목록
- `POST /api/orders` — 주문 생성. 상품 가격은 서버가 Supabase에서 다시 조회해 계산합니다.

주문 요청 예시:

```json
{
  "customerName": "홍길동",
  "customerEmail": "buyer@example.com",
  "customerPhone": "010-1234-5678",
  "shippingAddress": "서울시 ...",
  "items": [
	{ "productId": "상품 UUID", "quantity": 2 }
  ]
}
```

## Supabase 설정

1. Supabase 프로젝트를 만든 뒤 SQL Editor에서 `supabase/schema.sql`을 실행합니다.
2. `products` 테이블에 상품을 추가합니다. API는 `is_active = true`인 상품만 반환합니다.
3. 프로젝트의 Project URL과 `service_role` 키를 Render의 환경 변수에 등록합니다. `service_role` 키는 절대로 GitHub에 커밋하거나 브라우저 코드에 넣지 마세요.

## Render 배포

1. 이 저장소를 GitHub에 푸시하고 Render에서 **New Web Service → Docker**로 연결합니다.
2. 환경 변수 `SUPABASE_URL`, `SUPABASE_SERVICE_ROLE_KEY`를 설정합니다. 앱은 Render의 `PORT` 환경 변수에 바인딩합니다.
3. Render에서 배포가 완료되면 `https://<서비스 주소>/api/health`를 확인합니다.
4. Render의 Custom Domains 설정에서 `daorson.co.kr`을 추가한 다음 안내되는 DNS 레코드를 도메인 DNS 관리 화면에 등록합니다. DNS 전파 후 HTTPS 상태를 확인합니다.

도메인 소유권, DNS 공급자 및 Render/Supabase 계정 접근 권한이 없어 실제 도메인 연결과 배포는 계정에서 별도로 완료해야 합니다.
