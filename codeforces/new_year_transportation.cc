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

int main(int argc, char *argv[]) {
  int n, t;
  std::cin >> n >> t;
  int cur = 1, pos = 1;

  while (pos < n && pos != t) {
    int buf;
    std::cin >> buf;

    if (pos == cur) {
      pos += buf;
    }

    cur++;
  }

  if (pos == t) {
    std::cout << "YES"
              << "\n";
  } else {
    std::cout << "NO"
              << "\n";
  }

  return 0;
}
