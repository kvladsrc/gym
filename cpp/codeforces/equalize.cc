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

void invert(char& c) {
  if (c == '1') {
    c = '0';
  } else {
    c = '1';
  }
}

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n;
  std::cin >> n;
  string a, b;
  std::cin >> a >> b;

  // 11 -> 00
  // 2 if two type 2 operations.
  // 1 if one type 1 operation.
  //
  // 111 -> 010
  // 2 if two type 2 operations.
  // 2 if one type 1 operation.
  //
  // Conclusion: only case when type one operation is more effective
  // that type 2 is consecutive elements.

  int res = 0;
  for (int idx = 0; idx < (n - 1); ++idx) {
    if (a[idx] == b[idx]) {
      continue;
    }

    if (a[idx + 1] != b[idx + 1] && a[idx] != a[idx + 1]) {
      invert(a[idx + 1]);
    }

    invert(a[idx]);
    res++;
  }

  if (a.back() != b.back()) {
    res++;
  }

  std::cout << res << "\n";

  return 0;
}
