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

int main(int argc, char* argv[]) {
  int n;
  std::cin >> n;
  vector<int64_t> pref_sum(n + 1);
  pref_sum[0] = 0;
  for (int i = 1; i <= n; ++i) {
    int64_t buf;
    std::cin >> buf;
    pref_sum[i] = pref_sum[i - 1] + buf;
  }

  int64_t check_point = 0;
  int start = 1, end = n;
  while (start < end) {
    int64_t sum3 = pref_sum.back() - pref_sum[end - 1];

    if (pref_sum[start] == sum3) {
      check_point = pref_sum[start];
      start++;
    }

    if (pref_sum[start] < sum3) {
      start++;
    } else {
      end--;
    }
  }

  std::cout << check_point << "\n";

  return 0;
}
