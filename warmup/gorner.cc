#include <cstdint>
#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

static int64_t gorner(vector<int64_t> &coefs, int64_t x) {
  int64_t res = 0;
  for (auto c : coefs) {
    res *= x;
    res += c;
  }
  return res;
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n = 0;
  std::cin >> n;
  vector<int64_t> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }
  int64_t x = 0;
  std::cin >> x;

  std::cout << gorner(a, x) << "\n";

  return 0;
}
