#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

#define MAX_B 1000000001;
#define MIN_B 0;

int main(int argc, char *argv[]) {
  int n;
  int64_t min_b = MAX_B;
  int64_t max_b = MIN_B;
  int64_t min_b_count = 0;
  int64_t max_b_count = 0;

  std::cin >> n;
  for (int i = 0; i < n; ++i) {
    int64_t buf;
    std::cin >> buf;

    if (buf < min_b) {
      min_b_count = 1;
      min_b = buf;
    } else {
      if (buf == min_b) min_b_count++;
    }

    if (buf > max_b) {
      max_b_count = 1;
      max_b = buf;
    } else {
      if (buf == max_b) max_b_count++;
    }
  }

  std::cout << max_b - min_b << " ";
  if (max_b == min_b) {
    std::cout << (max_b_count * (max_b_count - 1)) / 2 << "\n";
  } else {
    std::cout << max_b_count * min_b_count << "\n";
  }

  return 0;
}
