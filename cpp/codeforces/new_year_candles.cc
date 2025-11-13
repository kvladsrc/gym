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
  int a, b;
  std::cin >> a >> b;
  int hours = 0, wents = 0;
  while (a) {
    hours += a;
    wents += a;
    a = wents / b;
    wents %= b;
  }
  std::cout << hours << "\n";

  return 0;
}
