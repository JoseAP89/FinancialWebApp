select * from accounts where financialstatement='ASSET';
select * from accounts where financialstatement='LIABILITY';
select * from transactions;
select * from accounts where id in (60,62); -- 60 ="Checking Account", 62 "Credit Card"
select sum(amount) from transactionlines where accountid in ( 60,62);
select * from transactionlines where accountid in ( 60,62);

select tl.*, a.financialstatement from transactionlines tl
 inner join accounts a on tl.accountid = a.id ;
 select sum(tl.amount*tl.quantity) from transactionlines tl
 inner join accounts a on tl.accountid = a.id where a.financialstatement = 'EXPENSE';

SELECT *
FROM transactionlines order by id; 


