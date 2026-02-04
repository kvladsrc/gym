#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <memory>
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
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int64_t gcd(int64_t a, int64_t b) {
  if (a % b == 0) {
    return b;
  }

  return gcd(b, a % b);
}

int64_t lcm(int64_t a, int64_t b) { return (a * b) / gcd(a, b); }

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int64_t n, x, y;
    std::cin >> n >> x >> y;

    int64_t common = n / lcm(x, y);
    int64_t plus_num = n / x - common;
    int64_t minus_num = n / y - common;

    int64_t res = 0;
    res += ((n + (n - plus_num + 1)) * plus_num) / 2;
    res -= ((minus_num + 1) * minus_num) / 2;
    std::cout << res << "\n";
  }

  return 0;
}
