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

int minutes_before(int h, int m) {
  int in_minutes = h * 60 + m;
  int new_year = 24 * 60;
  return new_year - in_minutes;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int h, m;
    std::cin >> h >> m;
    std::cout << minutes_before(h, m) << "\n";
  }
  return 0;
}
