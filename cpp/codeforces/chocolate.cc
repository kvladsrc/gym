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
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n;
  std::cin >> n;

  vector<int> nut_pos;
  for (int64_t i = 0; i < n; ++i) {
    int buf;
    std::cin >> buf;
    if (buf) {
      nut_pos.push_back(i);
    }
  }

  if (nut_pos.size() <= 1) {
    std::cout << nut_pos.size() << "\n";
    return 0;
  }

  int64_t res = nut_pos[1] - nut_pos[0];
  for (size_t i = 2; i < nut_pos.size(); ++i) {
    res *= nut_pos[i] - nut_pos[i - 1];
  }
  std::cout << res << "\n";

  return 0;
}
