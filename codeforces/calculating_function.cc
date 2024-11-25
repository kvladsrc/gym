#include <iostream>

long long seq_sum(long long n) { return (((n + 1) * n) / 2); }

long long calculate(long long n) {
  bool odd = n % 2;
  long long first_seq = seq_sum(n / 2) * 2;
  long long second_seq = seq_sum(n / 2) * 2 - n / 2;
  long long result = first_seq + (-1 * second_seq);
  if (odd)
    return result - n;
  else
    return result;
}

long long calculate_naive(long long n) {
  // false: -, true: +
  bool sign = false;
  long long res = 0;
  for (int i = 1; i <= n; ++i) {
    if (sign) {
      res += i;
    } else {
      res -= i;
    }
    sign = !sign;
  }

  return res;
}

int main(int argc, char *argv[]) {
  long long n;
  std::cin >> n;
  std::cout << calculate(n) << "\n";

  return 0;
}
