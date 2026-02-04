#include <cstdint>
#include <iostream>

int64_t seq_sum(int64_t n) { return (((n + 1) * n) / 2); }

int64_t calculate(int64_t n) {
  bool odd = n % 2;
  int64_t first_seq = seq_sum(n / 2) * 2;
  int64_t second_seq = seq_sum(n / 2) * 2 - n / 2;
  int64_t result = first_seq + (-1 * second_seq);
  if (odd)
    return result - n;
  else
    return result;
}

int64_t calculate_naive(int64_t n) {
  // false: -, true: +
  bool sign = false;
  int64_t res = 0;
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

int main(int argc, char* argv[]) {
  int64_t n;
  std::cin >> n;
  std::cout << calculate(n) << "\n";

  return 0;
}
