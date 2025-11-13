#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
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

bool solve_fast(vector<int> &a) { return a.front() < a.back(); }

// O(n) = 2^n
bool solve(vector<int> &a) {
  if (a.size() == 1) {
    return true;
  }

  vector<int> case1;
  vector<int> case2;

  bool branched = false;
  case1.push_back(a[0]);
  case2.push_back(a[0]);
  for (size_t i = 1; i < a.size(); ++i) {
    case1.push_back(a[i]);
    case2.push_back(a[i]);

    if (branched) continue;

    if (a[i - 1] < a[i]) {
      // a = {10 1 2}
      // case2 = {10 2}
      case2.pop_back();
      case2.pop_back();
      case2.push_back(a[i]);

      // case1 = {10 1}
      case1.pop_back();
      branched = true;
    }
  }

  if (!branched) {
    return false;
  }

  return solve(case1) || solve(case2);
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    if (solve_fast(a)) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }

  return 0;
}
