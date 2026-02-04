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

int main(int argc, char* argv[]) {
  int n, m, d;
  std::cin >> n >> m >> d;

  // Let replace each element by el / d. If exists two elements a, b:
  // a % d != b % d then the answer is -1 (we cannot make them equal
  // by adding or substracting d. Lets count average of replaced
  // values and then count the number of operations for each element
  // to achive the value. Average or median?
  //
  // UPD. Average does not works.

  vector<int> nums(n * m);
  int r = -1;
  bool res = true;
  for (int idx = 0; idx < (n * m); ++idx) {
    int buf;
    std::cin >> buf;

    if (!res || (idx > 0 && (buf % d) != r)) {
      // We need to consume input.
      res = false;
      continue;
    } else {
      r = buf % d;
    }

    nums[idx] = buf / d;
  }

  if (!res) {
    std::cout << -1 << "\n";
    return 0;
  }

  sort(nums.begin(), nums.end());
  auto median_case1 = nums[((n * m) / 2) - 1];
  auto median_case2 = nums[((n * m) / 2)];

  int ops_case1 = 0, ops_case2 = 0;
  for (auto i : nums) {
    ops_case1 += abs(median_case1 - i);
    ops_case2 += abs(median_case2 - i);
  }
  std::cout << min(ops_case2, ops_case1) << "\n";

  return 0;
}
