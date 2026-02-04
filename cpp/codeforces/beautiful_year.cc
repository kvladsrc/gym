#include <iostream>
#include <vector>

using std::vector;

bool is_beautiful(int n) {
  vector<bool> used(10);
  while (n) {
    if (used[n % 10]) return false;
    used[n % 10] = true;
    n /= 10;
  }
  return true;
}

int main(int argc, char* argv[]) {
  int year;
  std::cin >> year;
  year++;
  while (true) {
    if (is_beautiful(year)) break;
    year++;
  }
  std::cout << year << "\n";

  return 0;
}
