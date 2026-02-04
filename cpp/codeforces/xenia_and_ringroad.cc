#include <algorithm>
#include <cstdint>
#include <iostream>
#include <set>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int64_t n, m;
  std::cin >> n >> m;
  int64_t cur = 1;
  int64_t time = 0;
  while (m) {
    int64_t task;
    std::cin >> task;

    if (cur <= task) {
      time += task - cur;
    } else {
      time += task + (n - cur);
    }

    cur = task;
    m--;
  }

  std::cout << time << "\n";

  return 0;
}
