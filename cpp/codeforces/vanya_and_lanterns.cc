#include <algorithm>
#include <cstdint>
#include <iomanip>
#include <iostream>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::pair;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

double min_rad(vector<int64_t>& ls, int64_t l) {
  sort(ls.begin(), ls.end());
  double min_dist = max(ls[0], l - ls[ls.size() - 1]);

  int prev = ls[0];
  for (int lan : ls) {
    if ((lan - prev) / 2.0 > min_dist) min_dist = (lan - prev) / 2.0;
    prev = lan;
  }

  return min_dist;
}

int main(int argc, char* argv[]) {
  int64_t n, l;
  std::cin >> n >> l;
  vector<int64_t> ls(n);
  for (int64_t i = 0; i < n; ++i) {
    std::cin >> ls[i];
  }

  std::cout << std::fixed << std::setprecision(10) << min_rad(ls, l) << "\n";

  return 0;
}
