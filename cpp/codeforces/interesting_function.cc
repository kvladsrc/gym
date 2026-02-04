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

int64_t d(int64_t i) {
  int64_t res = 0;
  while (i) {
    res += i;
    i /= 10;
  }
  return res;
}

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int l, r;
    std::cin >> l >> r;

    // Idea is to implement the function count number of digit changes
    // from 0 to n -- D(n). Result is D(r) - D(l).
    std::cout << d(r) - d(l) << "\n";
  }

  return 0;
}
