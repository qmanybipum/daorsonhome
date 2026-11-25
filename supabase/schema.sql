create table if not exists public.products (
	id uuid primary key default gen_random_uuid(),
	name text not null check (char_length(name) between 1 and 200),
	description text,
	price numeric(12, 2) not null check (price >= 0),
	image_url text,
	is_active boolean not null default true,
	created_at timestamptz not null default now()
);

create table if not exists public.orders (
	id uuid primary key default gen_random_uuid(),
	customer_name text not null,
	customer_email text not null,
	customer_phone text not null,
	shipping_address text not null,
	total_amount numeric(12, 2) not null check (total_amount >= 0),
	status text not null default 'pending' check (status in ('pending', 'paid', 'shipped', 'cancelled')),
	created_at timestamptz not null default now()
);

create table if not exists public.order_items (
	id bigint generated always as identity primary key,
	order_id uuid not null references public.orders(id) on delete cascade,
	product_id uuid not null references public.products(id),
	product_name text not null,
	unit_price numeric(12, 2) not null check (unit_price >= 0),
	quantity integer not null check (quantity between 1 and 10)
);

create or replace function public.create_order(
	p_customer_name text,
	p_customer_email text,
	p_customer_phone text,
	p_shipping_address text,
	p_items jsonb
)
returns uuid
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare
	v_order_id uuid;
	v_product public.products%rowtype;
	v_item jsonb;
	v_quantity integer;
	v_total numeric(12, 2) := 0;
begin
	if p_items is null or jsonb_typeof(p_items) is distinct from 'array' then
		raise exception 'Order items must be an array';
	end if;
	if jsonb_array_length(p_items) not between 1 and 30 then
		raise exception 'An order must contain between 1 and 30 items';
	end if;

	insert into public.orders (
		customer_name, customer_email, customer_phone, shipping_address, total_amount
	) values (
		p_customer_name, p_customer_email, p_customer_phone, p_shipping_address, 0
	) returning id into v_order_id;

	for v_item in select value from jsonb_array_elements(p_items)
	loop
		v_quantity := (v_item ->> 'quantity')::integer;
		if v_quantity not between 1 and 10 then
			raise exception 'Item quantity must be between 1 and 10';
		end if;

		select * into v_product
		from public.products
		where id = (v_item ->> 'productId')::uuid and is_active = true
		for share;

		if not found then
			raise exception 'An order item is unavailable';
		end if;

		insert into public.order_items (order_id, product_id, product_name, unit_price, quantity)
		values (v_order_id, v_product.id, v_product.name, v_product.price, v_quantity);

		v_total := v_total + v_product.price * v_quantity;
	end loop;

	update public.orders set total_amount = v_total where id = v_order_id;
	return v_order_id;
end;
$$;

revoke all on function public.create_order(text, text, text, text, jsonb) from public, anon, authenticated;
grant execute on function public.create_order(text, text, text, text, jsonb) to service_role;
grant select on public.products to service_role;
