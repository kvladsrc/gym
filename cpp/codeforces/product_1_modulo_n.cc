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

int64_t gcd(int64_t a, int64_t b) {
  if (a % b == 0) {
    return abs(b);
  }
  return gcd(b, a % b);
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int64_t n = 0;
  std::cin >> n;

  // If gcd(n, a) != 1, a is multiplier of x, x % n != 1, because of
  // Euclid theorem.
  //
  // Let P is a product of all i, 1 <= i < n, gcd(i, n) = 1;

  int64_t prod = 1;
  vector<int64_t> m;

  for (int64_t a = 1; a < n; ++a) {
    if (gcd(a, n) == 1) {
      prod *= a;
      prod %= n;
      m.push_back(a);
    }
  }

  if (prod != 1) {
    m.pop_back();
  }

  std::cout << m.size() << "\n";
  for (auto i : m) {
    std::cout << i << " ";
  }
  std::cout << "\n";

  return 0;
}
