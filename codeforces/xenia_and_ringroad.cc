#include <algorithm>
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

int main(int argc, char *argv[]) {
  long long n, m;
  std::cin >> n >> m;
  long long cur = 1;
  long long time = 0;
  while (m) {
    long long task;
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
