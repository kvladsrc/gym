#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
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
using std::gcd;
using std::map;
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

int main(int argc, char* argv[]) {
  uint64_t n, k;
  std::cin >> n >> k;

  set<uint64_t> divisors;
  for (uint64_t d = 1; (d * d) <= n; ++d) {
    if (n % d) {
      continue;
    }
    divisors.insert(d);
    divisors.insert(n / d);
  }

  if (divisors.size() < k) {
    std::cout << -1 << "\n";
  } else {
    auto it = divisors.begin();
    std::advance(it, k - 1);
    std::cout << *it << "\n";
  }

  return 0;
}
