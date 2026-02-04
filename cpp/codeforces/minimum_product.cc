#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int64_t solve(int64_t a, int64_t b, int64_t x, int64_t y, int64_t n) {
  int64_t old_b = b;
  b = max(b - n, y);
  n -= old_b - b;
  a = max(a - n, x);
  return a * b;
}

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int64_t a, b, x, y, n;
    std::cin >> a >> b >> x >> y >> n;

    auto case1 = solve(a, b, x, y, n);
    auto case2 = solve(b, a, y, x, n);

    std::cout << (case1 < case2 ? case1 : case2) << "\n";
  }

  return 0;
}
