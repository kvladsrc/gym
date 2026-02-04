#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
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
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int n;
  std::cin >> n;

  vector<int> ex(3, 0);
  for (int i = 0; i < n; ++i) {
    int buf;
    std::cin >> buf;
    ex[i % 3] += buf;
  }

  if (ex[0] > ex[1] && ex[0] > ex[2]) {
    std::cout << "chest"
              << "\n";
  } else if (ex[1] > ex[0] && ex[1] > ex[2]) {
    std::cout << "biceps"
              << "\n";
  } else {
    std::cout << "back"
              << "\n";
  }

  return 0;
}
