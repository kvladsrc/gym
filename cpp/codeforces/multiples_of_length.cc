#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  size_t n = 0;
  std::cin >> n;
  vector<int64_t> a(n);
  for (auto& i : a) {
    std::cin >> i;
  }

  // All segments must be different: if we can make all elements from
  // l to r by 2 operations we can make them zero by 1 oparation. a +
  // b = c, if a is multiples of d and b multiples of d -> c is
  // multiples of d.
  //
  // UPD. What a nice problem!
  //
  // 1. For every for every a, b, c, gcd(a,b) = 1, exists such a x and y,
  // xa + yb = c.
  // 2. GCD(a, a+1) = 1;
  //
  // Using these lammas we can find the solution: a = n, b = n - 1,
  // for every C_i, 1 <= i < n we can find such a x and y to make
  // elements equal to zero using 2 operations. Then we can use third
  // operation to make C_n element equal to zero.

  if (n == 1) {
    std::cout << "1 1\n";
    std::cout << -a.front() << "\n";
    std::cout << "1 1\n";
    std::cout << "0\n";
    std::cout << "1 1\n";
    std::cout << "0\n";

    return 0;
  }

  vector<int64_t> x(n);
  vector<int64_t> y(n);

  for (size_t idx = 0; idx < n - 1; ++idx) {
    // a[idx] + a[idx] * (n - 1) = a[idx](1 + n - 1) = a[idx] * n.
    x[idx] = a[idx] * static_cast<int64_t>(n - 1);
    y[idx] = -(a[idx]) * static_cast<int64_t>(n);
  }

  std::cout << 1 << " " << n - 1 << "\n";
  for (size_t idx = 0; idx < n - 1; ++idx) {
    std::cout << x[idx] << " ";
  }
  std::cout << "\n";

  y.back() = 0;
  std::cout << 1 << " " << n << "\n";
  for (size_t idx = 0; idx < n; ++idx) {
    std::cout << y[idx] << " ";
  }
  std::cout << "\n";

  std::cout << n << " " << n << "\n";
  std::cout << -a.back() << "\n";

  return 0;
}
